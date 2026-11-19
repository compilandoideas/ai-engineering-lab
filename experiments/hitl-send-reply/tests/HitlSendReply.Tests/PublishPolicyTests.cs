using HitlSendReply.Domain;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.Publishing;

namespace HitlSendReply.Tests;

public class PublishPolicyTests
{
    private const string SafeText = "According to our system, your order #A-1042 shipped on November 12.";

    private static (PublishPolicy Policy, FakeOutbox Outbox) Create(IReplyJudge judge, TimeSpan? timeout = null)
    {
        var outbox = new FakeOutbox(TimeProvider.System);
        var policy = new PublishPolicy(SampleTickets.Store(), AgentPermissions.EmailOnly("SupportAgent"), outbox, judge,
            new PublishPolicyOptions("SupportAgent", timeout ?? TimeSpan.FromSeconds(5)));
        return (policy, outbox);
    }

    [Fact]
    public async Task Verified_facts_and_clear_judge_auto_send()
    {
        var (policy, _) = Create(ScriptedJudge.AlwaysClear());

        var decision = await policy.EvaluateAsync(new ReplyRequest("T-1001", SafeText));

        Assert.Equal(PolicyOutcome.AutoSend, decision.Outcome);
    }

    [Theory]
    [InlineData("T-9999", PolicyStage.Ticket)]   // unknown ticket
    [InlineData("T-1005", PolicyStage.Ticket)]   // closed ticket
    [InlineData("T-1004", PolicyStage.Permissions)] // chat channel, agent is email-only
    public async Task Blocks_without_a_human(string ticketId, PolicyStage stage)
    {
        var judge = ScriptedJudge.AlwaysClear();
        var (policy, _) = Create(judge);

        var decision = await policy.EvaluateAsync(new ReplyRequest(ticketId, "Thanks for your message."));

        Assert.Equal(PolicyOutcome.Block, decision.Outcome);
        Assert.Equal(stage, decision.Stage);
        Assert.Equal(0, judge.Calls);
    }

    [Fact]
    public async Task Blocks_a_duplicate_of_an_already_sent_text()
    {
        var (policy, outbox) = Create(ScriptedJudge.AlwaysClear());
        var request = new ReplyRequest("T-1001", SafeText);
        outbox.TrySend(request.Key, "ana.perez@example.com", Channel.Email, SafeText);

        var decision = await policy.EvaluateAsync(request);

        Assert.Equal(PolicyOutcome.Block, decision.Outcome);
        Assert.Equal(PolicyStage.Duplicate, decision.Stage);
    }

    [Fact]
    public async Task A_clear_judge_cannot_override_unverified_facts()
    {
        var judge = ScriptedJudge.AlwaysClear();
        var (policy, _) = Create(judge);

        var decision = await policy.EvaluateAsync(new ReplyRequest("T-1001", "We will refund $500 today."));

        Assert.Equal(PolicyOutcome.NeedsHuman, decision.Outcome);
        Assert.Equal(PolicyStage.Facts, decision.Stage);
        Assert.Equal(0, judge.Calls);
    }

    [Fact]
    public async Task Judge_escalation_goes_to_a_human()
    {
        var (policy, _) = Create(ScriptedJudge.AlwaysEscalate(JudgeCategory.Commitment, "Promises a refund."));

        var decision = await policy.EvaluateAsync(new ReplyRequest("T-1002", "This was our fault and we will refund you."));

        Assert.Equal(PolicyOutcome.NeedsHuman, decision.Outcome);
        Assert.Equal(PolicyStage.Judge, decision.Stage);
    }

    [Fact]
    public async Task Judge_error_goes_to_a_human()
    {
        var (policy, _) = Create(new FailingJudge(new HttpRequestException("down")));

        var decision = await policy.EvaluateAsync(new ReplyRequest("T-1001", SafeText));

        Assert.Equal(PolicyOutcome.NeedsHuman, decision.Outcome);
        Assert.Equal(PolicyStage.Error, decision.Stage);
    }

    [Fact]
    public async Task Judge_timeout_goes_to_a_human()
    {
        var (policy, _) = Create(new HangingJudge(), TimeSpan.FromMilliseconds(50));

        var decision = await policy.EvaluateAsync(new ReplyRequest("T-1001", SafeText));

        Assert.Equal(PolicyOutcome.NeedsHuman, decision.Outcome);
        Assert.Contains("timed out", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_judge_output_produces_auto_send_when_a_verifiable_check_fails()
    {
        // Every way the judge can answer, against every deterministic failure.
        IReplyJudge[] judges =
        [
            ScriptedJudge.AlwaysClear(),
            ScriptedJudge.AlwaysEscalate(JudgeCategory.Other, "x"),
            new FailingJudge(new InvalidOperationException()),
        ];
        ReplyRequest[] failing =
        [
            new("T-9999", SafeText),
            new("T-1004", "Your order #A-1120 shipped on November 14."),
            new("T-1001", "Your order shipped on November 14."),
            new("T-1001", "Arrives tomorrow."),
        ];

        foreach (var judge in judges)
        foreach (var request in failing)
        {
            var (policy, _) = Create(judge);
            Assert.NotEqual(PolicyOutcome.AutoSend, (await policy.EvaluateAsync(request)).Outcome);
        }
    }
}
