using HitlSendReply.Api.Runs;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.SendReply;
using HitlSendReply.Tests.Fakes;
using HitlSendReply.Tickets;

namespace HitlSendReply.Tests.Lab;

/// <summary>
/// The seven designed scenarios, run through the real Microsoft Agent Framework pipeline
/// (ChatClientAgent + ApprovalRequiredAIFunction + UseToolApproval) with a scripted model.
/// </summary>
public class ScenarioTests
{
    private static Scenario Get(int number, string lang) =>
        ScenarioCatalog.For(lang == "es" ? Language.Es : Language.En).Single(s => s.Number == number);

    private static ScriptedApprover Approves() => new(_ => HumanVerdict.Approve("reviewer@test"));

    private static ScriptedApprover Rejects(string reason = "Not acceptable.") => new(_ => HumanVerdict.Reject("reviewer@test", reason));

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S1_verified_fact_is_sent_without_a_human(string lang)
    {
        var scenario = Get(1, lang);
        var approver = Approves();
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver);

        var result = await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Empty(approver.Prompts);
        Assert.Equal(0, result.ApprovalRounds);
        var sent = Assert.Single(pipeline.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
        Assert.Equal(scenario.Ticket.CustomerAddress, sent.Recipient);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "sent" && e.Actor == "policy");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S2_refund_promise_waits_for_a_human_and_nothing_is_sent_if_rejected(string lang)
    {
        var scenario = Get(2, lang);
        var approver = Rejects("Do not promise refunds; say a teammate will review.");
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var prompt = Assert.Single(approver.Prompts);
        Assert.Equal(PolicyStage.Judge, prompt.Decision.Stage);
        Assert.Equal(scenario.Drafts[0], prompt.Text);
        Assert.Equal(scenario.Ticket.CustomerAddress, prompt.Recipient);
        Assert.Equal(Channel.Email, prompt.Channel);
        Assert.Empty(pipeline.Outbox.Sent);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "human-rejected" && e.Reason!.StartsWith("Do not promise", StringComparison.Ordinal));
        Assert.Equal(scenario.Ticket.Language, pipeline.Tickets.Find(scenario.Ticket.Id)!.Language);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S2_refund_promise_is_sent_once_a_human_approves(string lang)
    {
        var scenario = Get(2, lang);
        var (pipeline, _) = ScenarioHarness.Build(scenario, Approves());

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var sent = Assert.Single(pipeline.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "sent" && e.Actor == "reviewer@test");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S3_ambiguous_reply_goes_to_a_human(string lang)
    {
        var scenario = Get(3, lang);
        var approver = Rejects();
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Equal(PolicyStage.Judge, Assert.Single(approver.Prompts).Decision.Stage);
        Assert.Empty(pipeline.Outbox.Sent);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S4_prompt_injection_is_never_auto_sent_even_if_the_judge_is_fooled(string lang)
    {
        var scenario = Get(4, lang);
        var approver = Rejects();
        var (pipeline, model) = ScenarioHarness.Build(scenario, approver);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var prompt = Assert.Single(approver.Prompts);
        Assert.Equal(PolicyStage.Facts, prompt.Decision.Stage);
        // The attacker's address never becomes the recipient: it comes from the ticket.
        Assert.Equal(scenario.Ticket.CustomerAddress, prompt.Recipient);
        Assert.DoesNotContain(pipeline.Audit.Entries, e => e.Recipient?.Contains("attacker", StringComparison.Ordinal) == true);
        Assert.Empty(pipeline.Outbox.Sent);
        // The injected text did reach the model (it is the customer's message), so the defense is not "hide it".
        Assert.Contains(model.Requests[0], m => m.Text.Contains(lang == "es" ? "Ignora tus reglas" : "Ignore your rules", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S5_a_tampered_approval_cannot_change_the_text_that_is_sent(string lang)
    {
        var scenario = Get(5, lang);
        var (pipeline, _) = ScenarioHarness.Build(scenario, Approves());

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.DoesNotContain(pipeline.Outbox.Sent, s => s.Text == scenario.TamperedText);
        var sent = Assert.Single(pipeline.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S5_with_chat_client_binding_disabled_the_tool_approval_middleware_still_binds(string lang)
    {
        // Observed in Microsoft.Agents.AI 1.28.0: UseToolApproval (ToolApprovalAgent) also rebinds each
        // approval response to the request it surfaced, so turning off DisableApprovalResponseBinding
        // alone does not let the swapped text through.
        var scenario = Get(5, lang);
        var (pipeline, _) = ScenarioHarness.Build(scenario, Approves(), disableApprovalResponseBinding: true);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var sent = Assert.Single(pipeline.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void S5_the_send_tool_refuses_text_nobody_authorized(string lang)
    {
        // Second layer: even if a call reached the tool directly, unapproved text is not published.
        var scenario = Get(5, lang);
        var (pipeline, _) = ScenarioHarness.Build(scenario, Approves());

        var result = pipeline.Tools.SendReply("T-1002", scenario.TamperedText);

        Assert.StartsWith("NOT SENT", result, StringComparison.Ordinal);
        Assert.Empty(pipeline.Outbox.Sent);
        Assert.Contains(pipeline.Audit.Entries, e => e.Event == "send-refused");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S6_duplicate_is_blocked_without_a_human_and_not_resent(string lang)
    {
        var scenario = Get(6, lang);
        var approver = Approves();
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Empty(approver.Prompts);
        Assert.Single(pipeline.Outbox.Sent); // only the earlier delivery
        Assert.Contains(pipeline.Audit.Entries, e => e is { Event: "policy-decision", Outcome: "Block", Stage: "Duplicate" });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task S7_judge_down_goes_to_a_human(string lang)
    {
        var scenario = Get(7, lang);
        var approver = Rejects();
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver);

        await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Equal(PolicyStage.Error, Assert.Single(approver.Prompts).Decision.Stage);
        Assert.Empty(pipeline.Outbox.Sent);
    }

    [Fact]
    public async Task Rejected_draft_is_rewritten_and_the_new_call_goes_through_the_whole_policy_again()
    {
        const string risky = "This was our mistake, we will refund you.";
        const string safe = "Our records show that order #A-1077 was delivered on November 7. A teammate will review your request.";
        var judge = new ScriptedJudge(text => text == risky
            ? JudgeSignal.EscalateFor(JudgeCategory.Commitment, "Promises a refund.")
            : JudgeSignal.Clear());
        var approver = new ScriptedApprover(p => p.Text == risky
            ? HumanVerdict.Reject("reviewer@test", "No refund promises.")
            : HumanVerdict.Approve("reviewer@test"));
        var pipeline = Pipeline.Create(new ScriptedChatClient("T-1002", [risky, safe]), judge, approver);

        var result = await pipeline.Runner.RunAsync("T-1002", "Damaged package, refund me.");

        // The rewrite is a new call that goes through the whole policy again. Because a reviewer
        // rejected a draft on this ticket, it is not auto-sent: a human sees the final text.
        Assert.Equal(2, approver.Prompts.Count);
        Assert.Equal(1, judge.Calls); // the rewrite stops at PriorRejection, before the judge
        var sent = Assert.Single(pipeline.Outbox.Sent);
        Assert.Equal(safe, sent.Text);
        Assert.Equal(2, result.ApprovalRounds);
        Assert.Equal(
            ["policy-decision:NeedsHuman", "human-rejected:Rejected", "policy-decision:NeedsHuman", "human-approved:Approved", "sent:"],
            pipeline.Audit.Entries.Select(e => $"{e.Event}:{e.Outcome}"));
    }
}
