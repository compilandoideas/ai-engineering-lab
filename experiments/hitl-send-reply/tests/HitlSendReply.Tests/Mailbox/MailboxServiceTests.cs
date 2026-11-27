using HitlSendReply.Contracts;
using HitlSendReply.Tests.Fakes;
using HitlSendReply.Web.Client.Mailbox;
using HitlSendReply.Web.Mailbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace HitlSendReply.Tests.Mailbox;

/// <summary>
/// The simulated inbox against a real Postgres (Testcontainers), with the agent faked. Image from
/// TESTCONTAINERS_POSTGRES_IMAGE (default postgres:17), for registries that rate-limit.
/// </summary>
public sealed class MailboxServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder(Environment.GetEnvironmentVariable("TESTCONTAINERS_POSTGRES_IMAGE") ?? "postgres:17").Build();

    private readonly FakeNotifier _notifier = new();
    private readonly FakeRuns _runs = new();

    public async Task InitializeAsync()
    {
        if (new DockerFactAttribute().Skip is null)
        {
            await _postgres.StartAsync();
            await using var db = NewDb();
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (new DockerFactAttribute().Skip is null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private MailboxDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MailboxDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private MailboxService Service(MailboxDbContext db) =>
        new(db, _notifier, _runs, TimeProvider.System, NullLogger<MailboxService>.Instance);

    private static ComposeEmailRequest Compose(string from = "li.wang@example.com", string subject = "Damaged package") =>
        new(from, subject, "The package arrived damaged.");

    [DockerFact]
    public async Task A_matched_email_is_saved_handed_to_the_agent_and_shows_the_run_status()
    {
        _runs.Status = EmailStatus.AwaitingReview;
        await using var db = NewDb();

        var row = await Service(db).SubmitAsync(Compose(), CancellationToken.None);

        Assert.Equal(EmailStatus.AwaitingReview, row.Status);
        Assert.Equal("run-1", row.RunId);
        Assert.Equal(Web.Client.Mailbox.Mailbox.SupportAddress, row.To);
        Assert.Equal(row.Id.ToString("n"), Assert.Single(_notifier.Received).EmailId);
        await using var check = NewDb();
        Assert.Equal(EmailStatus.AwaitingReview, (await check.Emails.SingleAsync()).Status);
    }

    [DockerFact]
    public async Task An_email_the_agent_cannot_match_is_unmatched_with_the_reason()
    {
        _notifier.Result = new InboundEmailResult(InboundEmailStatus.Unmatched, null, null, "No ticket for this sender or subject.");
        await using var db = NewDb();

        var row = await Service(db).SubmitAsync(Compose("someone@example.org"), CancellationToken.None);

        Assert.Equal(EmailStatus.Unmatched, row.Status);
        Assert.Equal("No ticket for this sender or subject.", row.Detail);
    }

    [DockerFact]
    public async Task When_the_agent_is_down_the_email_is_kept_and_a_retry_hands_it_over()
    {
        _notifier.Failure = new HttpRequestException("Connection refused");
        await using var db = NewDb();
        var service = Service(db);

        var failed = await service.SubmitAsync(Compose(), CancellationToken.None);
        _notifier.Failure = null;
        _runs.Status = EmailStatus.AwaitingReview;
        var retried = await service.RetryAsync(failed.Id, CancellationToken.None);

        Assert.Equal(EmailStatus.NotifyFailed, failed.Status);
        Assert.Equal("Connection refused", failed.Detail);
        Assert.Equal(EmailStatus.AwaitingReview, retried!.Status);
        Assert.All(_notifier.Received, r => Assert.Equal(failed.Id.ToString("n"), r.EmailId)); // same id: idempotent
    }

    [DockerFact]
    public async Task A_timeout_from_the_resilience_pipeline_also_leaves_the_email_as_notify_failed()
    {
        // Found running the app: with the API stopped, Polly's TimeoutRejectedException (not an
        // HttpRequestException) escaped and the request failed with 500.
        _notifier.Failure = new Polly.Timeout.TimeoutRejectedException("The operation didn't complete within the allowed timeout.");
        await using var db = NewDb();

        var row = await Service(db).SubmitAsync(Compose(), CancellationToken.None);

        Assert.Equal(EmailStatus.NotifyFailed, row.Status);
    }

    [DockerFact]
    public async Task A_retry_does_not_hand_over_an_email_twice()
    {
        _runs.Status = EmailStatus.AwaitingReview;
        await using var db = NewDb();
        var service = Service(db);
        var row = await service.SubmitAsync(Compose(), CancellationToken.None);

        await service.RetryAsync(row.Id, CancellationToken.None);

        Assert.Single(_notifier.Received);
    }

    [DockerFact]
    public async Task Listing_reads_back_runs_that_finished()
    {
        _runs.Status = EmailStatus.AwaitingReview;
        await using (var db = NewDb())
        {
            await Service(db).SubmitAsync(Compose(), CancellationToken.None);
        }

        _runs.Status = EmailStatus.Sent;
        await using var list = NewDb();
        var page = await Service(list).ListAsync(new EmailQuery(null, null, false, 0, 10), CancellationToken.None);

        Assert.Equal(EmailStatus.Sent, Assert.Single(page.Items).Status);
    }

    [DockerFact]
    public async Task A_run_that_failed_to_start_shows_why()
    {
        _runs.Status = EmailStatus.Notified; // accepted, the agent is still drafting
        await using (var db = NewDb())
        {
            Assert.Equal(EmailStatus.Notified, (await Service(db).SubmitAsync(Compose(), CancellationToken.None)).Status);
        }

        _runs.Status = EmailStatus.RunFailed;
        _runs.Error = "model endpoint unavailable";
        await using var list = NewDb();
        var row = Assert.Single((await Service(list).ListAsync(new EmailQuery(null, null, false, 0, 10), CancellationToken.None)).Items);

        Assert.Equal(EmailStatus.RunFailed, row.Status);
        Assert.Equal("model endpoint unavailable", row.Detail);
    }

    [DockerFact]
    public async Task A_run_the_api_forgot_keeps_its_status_and_says_why()
    {
        _runs.Status = EmailStatus.AwaitingReview;
        await using (var db = NewDb())
        {
            await Service(db).SubmitAsync(Compose(), CancellationToken.None);
        }

        _runs.Status = null; // the API restarted
        await using var list = NewDb();
        var row = Assert.Single((await Service(list).ListAsync(new EmailQuery(null, null, false, 0, 10), CancellationToken.None)).Items);

        Assert.Equal(EmailStatus.AwaitingReview, row.Status);
        Assert.Equal(MailboxService.RunGone, row.Detail);
    }

    [DockerFact]
    public async Task Listing_searches_sorts_and_pages_on_the_server()
    {
        await using var db = NewDb();
        var service = Service(db);
        foreach (var i in Enumerable.Range(1, 12))
        {
            await service.SubmitAsync(Compose($"customer{i:00}@example.com", i % 3 == 0 ? $"Refund {i}" : $"Where is my order {i}"), CancellationToken.None);
        }

        var firstPage = await service.ListAsync(new EmailQuery(null, "from", false, 0, 5), CancellationToken.None);
        var lastPage = await service.ListAsync(new EmailQuery(null, "from", false, 2, 5), CancellationToken.None);
        var refunds = await service.ListAsync(new EmailQuery("refund", null, false, 0, 10), CancellationToken.None);

        Assert.Equal(12, firstPage.Total);
        Assert.Equal(["customer01@example.com", "customer02@example.com", "customer03@example.com", "customer04@example.com", "customer05@example.com"],
            firstPage.Items.Select(e => e.From));
        Assert.Equal(2, lastPage.Items.Count);
        Assert.Equal(4, refunds.Total);
    }

    [DockerFact]
    public async Task Categories_filter_by_where_each_email_stands_and_count_every_category()
    {
        await using var db = NewDb();
        var service = Service(db);
        foreach (var status in new[] { EmailStatus.AwaitingReview, EmailStatus.AwaitingReview, EmailStatus.Sent, EmailStatus.Blocked })
        {
            _runs.Status = status;
            await service.SubmitAsync(Compose(subject: $"{status}"), CancellationToken.None);
        }

        _notifier.Failure = new HttpRequestException("agent down");
        await service.SubmitAsync(Compose(subject: "Never handed over"), CancellationToken.None);
        _runs.Status = EmailStatus.AwaitingReview;

        var review = await service.ListAsync(new EmailQuery(null, null, false, 0, 10, MailFolder.Review), CancellationToken.None);
        var failed = await service.ListAsync(new EmailQuery(null, null, false, 0, 10, MailFolder.Failed), CancellationToken.None);
        int Count(MailFolder folder) => review.Folders!.Single(f => f.Folder == folder).Count;

        Assert.Equal(2, review.Total);
        Assert.All(review.Items, e => Assert.Equal(EmailStatus.AwaitingReview, e.Status));
        Assert.Equal("Never handed over", Assert.Single(failed.Items).Subject);
        Assert.Equal((5, 2, 0, 1, 1, 1), (Count(MailFolder.All), Count(MailFolder.Review), Count(MailFolder.Agent),
            Count(MailFolder.Sent), Count(MailFolder.NotSent), Count(MailFolder.Failed)));
    }

    [DockerFact]
    public async Task A_reply_waiting_for_a_person_is_kept_as_pending_once()
    {
        _runs.Pending.Add(new PendingReply("call-1", "li.wang@example.com", "We are sending a new package."));
        await using var db = NewDb();
        var service = Service(db);

        var row = await service.SubmitAsync(Compose(), CancellationToken.None);
        await service.RefreshInProgressAsync(CancellationToken.None);
        await service.GetAsync(row.Id, CancellationToken.None);

        Assert.Equal(ReplyStatus.Pending, row.Reply?.Status);
        Assert.Equal("We are sending a new package.", row.Reply?.Text);
        await using var check = NewDb();
        var stored = await check.Replies.SingleAsync();
        Assert.Equal((row.Id, "call-1", "run-1"), (stored.EmailId, stored.CallId, stored.RunId));
    }

    [DockerFact]
    public async Task An_approved_reply_keeps_who_approved_it_and_when()
    {
        _runs.Pending.Add(new PendingReply("call-1", "li.wang@example.com", "We are sending a new package."));
        await using var db = NewDb();
        var service = Service(db);
        var row = await service.SubmitAsync(Compose(), CancellationToken.None);

        var at = DateTimeOffset.Parse("2026-11-24T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        _runs.Pending.Clear();
        _runs.Verdicts.Add(new ReplyVerdict("call-1", true, null, "ana", at));
        _runs.Status = EmailStatus.Sent;
        var after = await service.GetAsync(row.Id, CancellationToken.None);

        Assert.Equal(EmailStatus.Sent, after!.Status);
        Assert.Equal((ReplyStatus.Approved, "ana", at), (after.Reply!.Status, after.Reply.Reviewer, after.Reply.DecidedAt));
    }

    [DockerFact]
    public async Task A_rejected_reply_keeps_the_reason_and_the_rewrite_waits_as_a_new_reply()
    {
        _runs.Pending.Add(new PendingReply("call-1", "li.wang@example.com", "First draft."));
        await using var db = NewDb();
        var service = Service(db);
        var row = await service.SubmitAsync(Compose(), CancellationToken.None);

        _runs.Pending.Clear();
        _runs.Pending.Add(new PendingReply("call-2", "li.wang@example.com", "Second draft."));
        _runs.Verdicts.Add(new ReplyVerdict("call-1", false, "Do not promise a date.", "ana", DateTimeOffset.UtcNow));
        var after = await service.GetAsync(row.Id, CancellationToken.None);

        Assert.Equal(("Second draft.", ReplyStatus.Pending), (after!.Reply!.Text, after.Reply.Status));
        await using var check = NewDb();
        var first = await check.Replies.SingleAsync(r => r.CallId == "call-1");
        Assert.Equal((ReplyStatus.Rejected, "Do not promise a date."), (first.Status, first.Reason));
        Assert.Equal(2, await check.Replies.CountAsync());
    }

    [DockerFact]
    public async Task An_edited_reply_keeps_the_draft_the_text_sent_and_the_mark()
    {
        _runs.Pending.Add(new PendingReply("call-1", "li.wang@example.com", "Agent draft."));
        await using var db = NewDb();
        var service = Service(db);
        var row = await service.SubmitAsync(Compose(), CancellationToken.None);

        _runs.Pending.Clear();
        _runs.Verdicts.Add(new ReplyVerdict("call-1", true, null, "ana", DateTimeOffset.UtcNow, Edited: true, FinalText: "Ana's version."));
        _runs.Status = EmailStatus.Sent;
        var reply = (await service.GetAsync(row.Id, CancellationToken.None))!.Reply!;

        Assert.Equal((ReplyStatus.Approved, true, "Agent draft.", "Ana's version."), (reply.Status, reply.Edited, reply.Text, reply.FinalText));
    }

    [Fact]
    public void The_first_decision_on_a_reply_is_the_one_kept()
    {
        var reply = new EmailReply { RunId = "run-1", CallId = "call-1", Recipient = "a@example.com", Text = "Hi" };

        Assert.True(reply.Decide(true, null, "ana", DateTimeOffset.UtcNow));
        Assert.False(reply.Decide(false, "late", "luis", DateTimeOffset.UtcNow));
        Assert.Equal((ReplyStatus.Approved, "ana"), (reply.Status, reply.Reviewer));
    }

    private sealed class FakeNotifier : IInboundEmailNotifier
    {
        public List<InboundEmailRequest> Received { get; } = [];
        public Exception? Failure { get; set; }
        public InboundEmailResult Result { get; set; } = new(InboundEmailStatus.Started, "run-1", "T-1002", null);

        public Task<InboundEmailResult> NotifyAsync(InboundEmail email, CancellationToken cancellationToken)
        {
            Received.Add(new InboundEmailRequest(email.Id.ToString("n"), email.From, email.Subject, email.Body, email.ReceivedAt));
            return Failure is null ? Task.FromResult(Result) : Task.FromException<InboundEmailResult>(Failure);
        }
    }

    private sealed class FakeRuns : IRunStatusReader
    {
        public EmailStatus? Status { get; set; } = EmailStatus.AwaitingReview;

        public string? Error { get; set; }

        public List<PendingReply> Pending { get; } = [];

        public List<ReplyVerdict> Verdicts { get; } = [];

        public Task<RunState?> ReadAsync(string runId, CancellationToken cancellationToken) =>
            Task.FromResult(Status is { } status ? new RunState(status, Error, [.. Pending], [.. Verdicts]) : null);
    }
}
