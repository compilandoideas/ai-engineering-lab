using HitlSendReply.Api.Runs;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.SendReply;

namespace HitlSendReply.Tests.SendReply;

/// <summary>
/// Over HTTP the person answers in a later request, so a run must stop at a review and continue
/// later with the verdict, with the same guarantees as the in-process loop.
/// </summary>
public class SupportRunnerResumeTests
{
    private const string Risky = "This was our mistake, we will refund you.";
    private const string Safe = "Our records show that order #A-1077 was delivered on November 7.";
    private const string Verified = "According to our system, your order #A-1042 shipped on November 12.";

    private static Pipeline Build(string ticketId, params string[] drafts) =>
        Pipeline.Create(new ScriptedChatClient(ticketId, drafts),
            new ScriptedJudge(text => text == Risky
                ? JudgeSignal.EscalateFor(JudgeCategory.Commitment, "Promises a refund.")
                : JudgeSignal.Clear()),
            human: null);

    [Fact]
    public async Task A_risky_draft_stops_the_run_with_a_pending_review_and_nothing_is_sent()
    {
        var pipeline = Build("T-1002", Risky);

        var run = await pipeline.Runner.StartAsync("T-1002", "Damaged package.");

        Assert.Equal(ApprovalRunStatus.AwaitingReview, run.Status);
        var review = Assert.Single(run.PendingReviews);
        Assert.Equal(Risky, review.Text);
        Assert.Equal("li.wang@example.com", review.Recipient);
        Assert.Equal(PolicyOutcome.NeedsHuman, review.Decision.Outcome);
        Assert.Empty(pipeline.Outbox.Sent);
    }

    [Fact]
    public async Task Resuming_with_an_approval_sends_the_reviewed_text_once()
    {
        var pipeline = Build("T-1002", Risky);
        var run = await pipeline.Runner.StartAsync("T-1002", "Damaged package.");

        await pipeline.Runner.ResumeAsync(run, run.PendingReviews[0].CallId, HumanVerdict.Approve("reviewer@test"));

        Assert.Equal(ApprovalRunStatus.Completed, run.Status);
        Assert.Equal(Risky, Assert.Single(pipeline.Outbox.Sent).Text);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "sent" && e.Actor == "reviewer@test");
    }

    [Fact]
    public async Task Resuming_with_a_rejection_sends_the_rewrite_to_a_human_too()
    {
        var pipeline = Build("T-1002", Risky, Safe);
        var run = await pipeline.Runner.StartAsync("T-1002", "Damaged package.");

        await pipeline.Runner.ResumeAsync(run, run.PendingReviews[0].CallId, HumanVerdict.Reject("reviewer@test", "No refund promises."));

        Assert.Equal(ApprovalRunStatus.AwaitingReview, run.Status);
        var rewrite = Assert.Single(run.PendingReviews);
        Assert.Equal(Safe, rewrite.Text);
        Assert.Equal(PolicyStage.PriorRejection, rewrite.Decision.Stage);
        Assert.Empty(pipeline.Outbox.Sent);
    }

    [Fact]
    public async Task A_verified_reply_completes_without_stopping()
    {
        var pipeline = Build("T-1001", Verified);

        var run = await pipeline.Runner.StartAsync("T-1001", "Where is my order?");

        Assert.Equal(ApprovalRunStatus.Completed, run.Status);
        Assert.Empty(run.PendingReviews);
        Assert.Equal(Verified, Assert.Single(pipeline.Outbox.Sent).Text);
    }

    [Fact]
    public async Task An_unknown_review_cannot_be_answered()
    {
        var pipeline = Build("T-1002", Risky);
        var run = await pipeline.Runner.StartAsync("T-1002", "Damaged package.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.Runner.ResumeAsync(run, "not-a-call", HumanVerdict.Approve("reviewer@test")));
        Assert.Empty(pipeline.Outbox.Sent);
    }

    [Fact]
    public async Task A_completed_run_cannot_be_resumed()
    {
        var pipeline = Build("T-1002", Risky);
        var run = await pipeline.Runner.StartAsync("T-1002", "Damaged package.");
        var callId = run.PendingReviews[0].CallId;
        await pipeline.Runner.ResumeAsync(run, callId, HumanVerdict.Approve("reviewer@test"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.Runner.ResumeAsync(run, callId, HumanVerdict.Approve("reviewer@test")));
        Assert.Single(pipeline.Outbox.Sent);
    }
}
