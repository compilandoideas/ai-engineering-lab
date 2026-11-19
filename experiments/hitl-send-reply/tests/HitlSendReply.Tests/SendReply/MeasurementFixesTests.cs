using HitlSendReply.Judge;
using HitlSendReply.Lab;
using HitlSendReply.Lab.Fakes;
using HitlSendReply.Policy;
using HitlSendReply.SendReply;
using HitlSendReply.Tickets;

namespace HitlSendReply.Tests.SendReply;

/// <summary>
/// Fixes for what the run with a real model (gpt-4.1-mini) showed on 2026-11-19:
/// a rewrite after a rejection auto-sent without a human seeing it; the agent re-sent a rejected
/// text; the fact checker ignored negation and flagged "shipped … and delivered" on a delivered order.
/// </summary>
public class MeasurementFixesTests
{
    // ── Rewrite after rejection ──────────────────────────────────────────────

    [Theory]
    [InlineData(Language.En, "This was our mistake, we will refund you.", "Our records show that order #A-1077 was delivered on November 7.")]
    [InlineData(Language.Es, "Fue nuestro error, le devolveremos el dinero.", "Nuestros registros indican que el pedido #A-1077 fue entregado el 7 de noviembre.")]
    public async Task After_a_rejection_the_rewrite_goes_to_a_human_even_if_it_would_auto_send(Language language, string risky, string safe)
    {
        // The safe rewrite passes facts and the judge: before the fix it auto-sent.
        var judge = new ScriptedJudge(text => text == risky
            ? JudgeSignal.EscalateFor(JudgeCategory.Commitment, "Promises a refund.")
            : JudgeSignal.Clear());
        var approver = new ScriptedApprover(p => p.Text == risky
            ? HumanVerdict.Reject("reviewer@test", "No refund promises.")
            : HumanVerdict.Approve("reviewer@test"));
        var pipeline = Pipeline.Create(new ScriptedChatClient("T-1002", [risky, safe]), judge, approver, language);

        await pipeline.Runner.RunAsync("T-1002", "Damaged package.");

        Assert.Equal([risky, safe], approver.Prompts.Select(p => p.Text));
        Assert.Equal(PolicyStage.PriorRejection, approver.Prompts[1].Decision.Stage);
        Assert.Equal(safe, Assert.Single(pipeline.Outbox.Sent).Text);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "sent" && e.Actor == "reviewer@test");
        Assert.DoesNotContain(pipeline.Audit.Entries, e => e.Outcome == "AutoSend");
    }

    [Theory]
    [InlineData(Language.En, "This was our mistake, we will refund you.")]
    [InlineData(Language.Es, "Fue nuestro error, le devolveremos el dinero.")]
    public async Task Resending_the_exact_rejected_text_is_blocked_without_asking_again(Language language, string risky)
    {
        var approver = new ScriptedApprover(_ => HumanVerdict.Reject("reviewer@test", "No refund promises."));
        var pipeline = Pipeline.Create(new ScriptedChatClient("T-1002", [risky, risky]), // the model re-sends the same text
            ScriptedJudge.AlwaysEscalate(JudgeCategory.Commitment, "Promises a refund."), approver, language);

        await pipeline.Runner.RunAsync("T-1002", "Damaged package.");

        Assert.Single(approver.Prompts);
        Assert.Empty(pipeline.Outbox.Sent);
        Assert.Contains(pipeline.Audit.Entries, e => e is { Outcome: "Block", Stage: "PriorRejection" });
    }

    [Fact]
    public async Task A_rejection_on_one_ticket_does_not_affect_another()
    {
        var reviews = new InMemoryReviewHistory();
        reviews.RecordRejection(ReplyKey.For("T-1002", "anything"), "no");
        var policy = new PublishPolicy(SampleTickets.Store(), AgentPermissions.EmailOnly("SupportAgent"),
            new FakeOutbox(TimeProvider.System), ScriptedJudge.AlwaysClear(),
            new PublishPolicyOptions("SupportAgent", TimeSpan.FromSeconds(5)), reviews);

        var decision = await policy.EvaluateAsync(
            new ReplyRequest("T-1001", "According to our system, your order #A-1042 shipped on November 12."));

        Assert.Equal(PolicyOutcome.AutoSend, decision.Outcome);
    }

    // ── Negation and status progression ──────────────────────────────────────

    private static Order Processing => SampleTickets.Get("T-1003").Order; // not shipped yet
    private static Order Delivered => SampleTickets.Get("T-1002").Order;  // shipped Nov 3, delivered Nov 7

    [Theory]
    [InlineData("Your order #A-1103 has not been shipped yet.")]
    [InlineData("Your order hasn't shipped yet; it is being prepared.")]
    public void English_negation_is_understood(string text) =>
        Assert.True(FactChecker.Check(text, Processing, Language.En).Verified);

    [Theory]
    [InlineData("Su pedido #A-1103 aún no ha sido enviado.")]
    [InlineData("Su pedido todavía no se ha despachado; está en preparación.")]
    [InlineData("Su pedido no se despachó todavía.")]
    public void Spanish_negation_is_understood(string text) =>
        Assert.True(FactChecker.Check(text, Processing, Language.Es).Verified);

    [Theory]
    [InlineData(Language.En, "Your order #A-1077 was shipped on November 3 and delivered on November 7.")]
    [InlineData(Language.Es, "Su pedido #A-1077 fue enviado el 3 de noviembre y entregado el 7 de noviembre.")]
    public void Shipped_then_delivered_is_true_for_a_delivered_order(Language language, string text) =>
        Assert.True(FactChecker.Check(text, Delivered, language).Verified);

    [Theory]
    [InlineData(Language.En, "Your order #A-1077 has not been shipped yet.")]          // it was delivered
    [InlineData(Language.Es, "Su pedido #A-1077 aún no ha sido enviado.")]
    [InlineData(Language.En, "Your order #A-1077 has not been delivered.")]
    [InlineData(Language.Es, "Su pedido #A-1077 no fue entregado.")]
    public void A_false_negated_claim_still_escalates(Language language, string text) =>
        Assert.False(FactChecker.Check(text, Delivered, language).Verified);

    [Theory]
    [InlineData(Language.En, "Your order #A-1103 has been shipped.")]   // still processing
    [InlineData(Language.Es, "Su pedido #A-1103 ya fue despachado.")]
    public void A_false_positive_claim_still_escalates(Language language, string text) =>
        Assert.False(FactChecker.Check(text, Processing, language).Verified);
}
