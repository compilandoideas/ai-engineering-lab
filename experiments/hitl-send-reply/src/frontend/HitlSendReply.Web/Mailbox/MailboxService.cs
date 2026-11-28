using HitlSendReply.Contracts;
using HitlSendReply.Web.Client.Mailbox;
using Microsoft.EntityFrameworkCore;

namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// The simulated inbox: save the email first, then hand it to the agent. If the agent cannot be
/// reached the email is kept as NotifyFailed and can be retried; it is never lost.
/// </summary>
public sealed partial class MailboxService(
    MailboxDbContext db, IInboundEmailNotifier notifier, IRunStatusReader runs, TimeProvider clock, ILogger<MailboxService> logger)
{
    private static readonly EmailStatus[] InProgress = [EmailStatus.Notified, EmailStatus.AwaitingReview];

    /// <summary>The Agent API keeps runs in memory: after it restarts, older runs are gone.</summary>
    public const string RunGone = "The Agent API no longer has this run (it keeps runs in memory and was restarted).";

    public async Task<EmailRowDto> SubmitAsync(ComposeEmailRequest request, CancellationToken cancellationToken)
    {
        var email = new InboundEmail
        {
            From = request.From.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            ReceivedAt = clock.GetUtcNow(),
        };
        db.Emails.Add(email);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyAsync(email, cancellationToken);
        return (await ToRowsAsync([email], cancellationToken))[0];
    }

    /// <summary>Null when the email does not exist; unchanged when it was already handed over.</summary>
    public async Task<EmailRowDto?> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await db.Emails.FindAsync([id], cancellationToken);
        if (email is null)
        {
            return null;
        }

        if (email.Status is EmailStatus.NotifyFailed or EmailStatus.Received)
        {
            await NotifyAsync(email, cancellationToken);
        }

        return (await ToRowsAsync([email], cancellationToken))[0];
    }

    public async Task<EmailRowDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await db.Emails.FindAsync([id], cancellationToken);
        if (email is null)
        {
            return null;
        }

        await RefreshAsync([email], cancellationToken);
        return (await ToRowsAsync([email], cancellationToken))[0];
    }

    public async Task<EmailPageDto> ListAsync(EmailQuery query, CancellationToken cancellationToken)
    {
        var emails = db.Emails.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            emails = emails.Where(e => EF.Functions.ILike(e.From, pattern) || EF.Functions.ILike(e.Subject, pattern)
                || EF.Functions.ILike(e.Body, pattern) || (e.TicketId != null && EF.Functions.ILike(e.TicketId, pattern)));
        }

        // Counted before the folder filter, so every category shows how many emails it holds.
        var byStatus = await emails.GroupBy(e => new { e.Status, Lost = e.Detail == RunGone })
            .Select(g => new { g.Key.Status, g.Key.Lost, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var folders = Enum.GetValues<MailFolder>()
            .Select(f => new FolderCountDto(f, byStatus.Where(s => f == MailFolder.All || FolderOf(s.Status, s.Lost) == f).Sum(s => s.Count)))
            .ToList();

        emails = InFolder(emails, query.Folder);
        var total = await emails.CountAsync(cancellationToken);
        var page = await Sort(emails, query.Sort, query.Descending)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        await RefreshAsync(page, cancellationToken);
        return new EmailPageDto(await ToRowsAsync(page, cancellationToken), total, folders);
    }

    /// <summary>
    /// Reads back every run still in progress, so a reply waiting for a person lands in
    /// email_replies even when nobody has the inbox open. Called by <see cref="PendingReplyTracker"/>.
    /// </summary>
    public async Task RefreshInProgressAsync(CancellationToken cancellationToken)
    {
        var emails = await db.Emails
            .Where(e => e.RunId != null && InProgress.Contains(e.Status) && (e.Detail == null || e.Detail != RunGone))
            .OrderBy(e => e.ReceivedAt).Take(50)
            .ToListAsync(cancellationToken);
        await RefreshAsync(emails, cancellationToken);
    }

    // Each row carries its latest reply: the one waiting now, or the last one decided.
    private async Task<List<EmailRowDto>> ToRowsAsync(IReadOnlyList<InboundEmail> emails, CancellationToken cancellationToken)
    {
        var ids = emails.Select(e => e.Id).ToList();
        var replies = await db.Replies.AsNoTracking().Where(r => ids.Contains(r.EmailId)).ToListAsync(cancellationToken);
        var latest = replies.GroupBy(r => r.EmailId).ToDictionary(g => g.Key, g => g.MaxBy(r => r.CreatedAt)!);
        return emails.Select(e => e.ToDto() with { Reply = latest.GetValueOrDefault(e.Id)?.ToDto() }).ToList();
    }

    // A run the agent lost (it keeps runs in memory) can no longer be reviewed or finish: it counts as failed.
    private static MailFolder FolderOf(EmailStatus status, bool lost) => (status, lost) switch
    {
        (EmailStatus.AwaitingReview or EmailStatus.Notified, true) => MailFolder.Failed,
        (EmailStatus.AwaitingReview, _) => MailFolder.Review,
        (EmailStatus.Notified, _) => MailFolder.Agent,
        (EmailStatus.Sent, _) => MailFolder.Sent,
        (EmailStatus.Blocked or EmailStatus.Unmatched, _) => MailFolder.NotSent,
        _ => MailFolder.Failed,
    };

    private static IQueryable<InboundEmail> InFolder(IQueryable<InboundEmail> emails, MailFolder folder) => folder switch
    {
        MailFolder.Review => emails.Where(e => e.Status == EmailStatus.AwaitingReview && (e.Detail == null || e.Detail != RunGone)),
        MailFolder.Agent => emails.Where(e => e.Status == EmailStatus.Notified && (e.Detail == null || e.Detail != RunGone)),
        MailFolder.Sent => emails.Where(e => e.Status == EmailStatus.Sent),
        MailFolder.NotSent => emails.Where(e => e.Status == EmailStatus.Blocked || e.Status == EmailStatus.Unmatched),
        MailFolder.Failed => emails.Where(e => e.Status == EmailStatus.NotifyFailed || e.Status == EmailStatus.Received
            || e.Status == EmailStatus.RunFailed || (InProgress.Contains(e.Status) && e.Detail == RunGone)),
        _ => emails,
    };

    private static IQueryable<InboundEmail> Sort(IQueryable<InboundEmail> emails, string? sort, bool descending) =>
        (sort?.ToLowerInvariant(), descending) switch
        {
            ("from", false) => emails.OrderBy(e => e.From).ThenByDescending(e => e.ReceivedAt),
            ("from", true) => emails.OrderByDescending(e => e.From).ThenByDescending(e => e.ReceivedAt),
            ("subject", false) => emails.OrderBy(e => e.Subject).ThenByDescending(e => e.ReceivedAt),
            ("subject", true) => emails.OrderByDescending(e => e.Subject).ThenByDescending(e => e.ReceivedAt),
            ("status", false) => emails.OrderBy(e => e.Status).ThenByDescending(e => e.ReceivedAt),
            ("status", true) => emails.OrderByDescending(e => e.Status).ThenByDescending(e => e.ReceivedAt),
            ("receivedat", false) => emails.OrderBy(e => e.ReceivedAt),
            _ => emails.OrderByDescending(e => e.ReceivedAt),
        };

    private async Task NotifyAsync(InboundEmail email, CancellationToken cancellationToken)
    {
        try
        {
            var result = await notifier.NotifyAsync(email, cancellationToken);
            email.TicketId = result.TicketId;
            email.RunId = result.RunId;
            email.Detail = result.Reason;
            email.Status = result.Status == InboundEmailStatus.Unmatched ? EmailStatus.Unmatched : EmailStatus.Notified;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Whatever failed (connection, timeout, resilience rejection, bad response), the email
            // stays in the inbox as NotifyFailed: it is never lost and can be retried.
            LogNotifyFailed(email.Id, ex.Message);
            email.Status = EmailStatus.NotifyFailed;
            email.Detail = ex.Message;
        }

        await db.SaveChangesAsync(CancellationToken.None);
        if (email.Status == EmailStatus.Notified)
        {
            await RefreshAsync([email], cancellationToken);
        }
    }

    // Rows whose run is still going are read back from the Agent API. If the API is down or no
    // longer knows the run (it keeps runs in memory), the row keeps its last known status.
    private async Task RefreshAsync(IEnumerable<InboundEmail> emails, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var email in emails.Where(e => e.RunId is not null && InProgress.Contains(e.Status) && e.Detail != RunGone))
        {
            try
            {
                var state = await runs.ReadAsync(email.RunId!, cancellationToken);
                if (state is null && email.Detail != RunGone)
                {
                    email.Detail = RunGone;
                    changed = true;
                }
                else if (state is not null)
                {
                    if (state.Status != email.Status || state.Error != email.Detail)
                    {
                        email.Status = state.Status;
                        email.Detail = state.Error;
                        changed = true;
                    }

                    changed |= await KeepRepliesAsync(email, state, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                LogRefreshFailed(email.Id, ex.Message);
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // A review the policy sent to a person is stored once as Pending; a person's verdict moves it
    // to Approved or Rejected, only from Pending, so the first decision is the one kept.
    private async Task<bool> KeepRepliesAsync(InboundEmail email, RunState state, CancellationToken cancellationToken)
    {
        var known = await db.Replies.Where(r => r.EmailId == email.Id).ToDictionaryAsync(r => r.CallId, cancellationToken);
        var changed = false;
        foreach (var pending in state.Pending ?? [])
        {
            if (!known.ContainsKey(pending.CallId))
            {
                var reply = new EmailReply
                {
                    EmailId = email.Id,
                    RunId = email.RunId!,
                    CallId = pending.CallId,
                    Recipient = pending.Recipient,
                    Text = pending.Text,
                    CreatedAt = clock.GetUtcNow(),
                };
                db.Replies.Add(reply);
                known[pending.CallId] = reply;
                changed = true;
            }
        }

        // A verdict for a review never seen pending has no text to keep, so it is skipped.
        foreach (var verdict in state.Verdicts ?? [])
        {
            if (known.TryGetValue(verdict.CallId, out var reply))
            {
                changed |= reply.Decide(verdict.Approved, verdict.Reason, verdict.Reviewer, verdict.At, verdict.Edited, verdict.FinalText);
            }
        }

        return changed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {EmailId} could not be handed to the agent: {Error}")]
    private partial void LogNotifyFailed(Guid emailId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run status for email {EmailId} not refreshed: {Error}")]
    private partial void LogRefreshFailed(Guid emailId, string error);
}

/// <summary>Server-side paging and sorting for the grid. Page is zero-based.</summary>
public sealed record EmailQuery(string? Search, string? Sort, bool Descending, int Page, int PageSize, MailFolder Folder = MailFolder.All);
