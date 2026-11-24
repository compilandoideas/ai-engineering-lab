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
        return email.ToDto();
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

        return email.ToDto();
    }

    public async Task<EmailRowDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await db.Emails.FindAsync([id], cancellationToken);
        if (email is null)
        {
            return null;
        }

        await RefreshAsync([email], cancellationToken);
        return email.ToDto();
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

        var total = await emails.CountAsync(cancellationToken);
        var page = await Sort(emails, query.Sort, query.Descending)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        await RefreshAsync(page, cancellationToken);
        return new EmailPageDto(page.Select(e => e.ToDto()).ToList(), total);
    }

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
                var status = await runs.ReadAsync(email.RunId!, cancellationToken);
                if (status is null && email.Detail != RunGone)
                {
                    email.Detail = RunGone;
                    changed = true;
                }
                else if (status is not null && status != email.Status)
                {
                    email.Status = status.Value;
                    changed = true;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {EmailId} could not be handed to the agent: {Error}")]
    private partial void LogNotifyFailed(Guid emailId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run status for email {EmailId} not refreshed: {Error}")]
    private partial void LogRefreshFailed(Guid emailId, string error);
}

/// <summary>Server-side paging and sorting for the grid. Page is zero-based.</summary>
public sealed record EmailQuery(string? Search, string? Sort, bool Descending, int Page, int PageSize);
