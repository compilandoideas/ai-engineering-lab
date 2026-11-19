using HitlSendReply.Agent;
using HitlSendReply.Domain;
using HitlSendReply.Judge;
using HitlSendReply.Policy;

namespace HitlSendReply.Tests;

/// <summary>
/// The seven designed scenarios, run through the real Microsoft Agent Framework pipeline
/// (ChatClientAgent + ApprovalRequiredAIFunction + UseToolApproval) with a scripted model.
/// </summary>
public class ScenarioTests
{
    private static Scenario Get(int number) => Scenarios.All.Single(s => s.Number == number);

    private static ScriptedApprover Approves() => new(_ => HumanVerdict.Approve("reviewer@test"));

    private static ScriptedApprover Rejects(string reason = "Not acceptable.") => new(_ => HumanVerdict.Reject("reviewer@test", reason));

    [Fact]
    public async Task S1_verified_fact_is_sent_without_a_human()
    {
        var scenario = Get(1);
        var approver = Approves();
        var (lab, _) = ScenarioHarness.Build(scenario, approver);

        var result = await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Empty(approver.Prompts);
        Assert.Equal(0, result.ApprovalRounds);
        var sent = Assert.Single(lab.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
        Assert.Equal(scenario.Ticket.CustomerAddress, sent.Recipient);
        Assert.Contains(lab.Audit.Entries, e => e.Event == "sent" && e.Actor == "policy");
    }

    [Fact]
    public async Task S2_refund_promise_waits_for_a_human_and_nothing_is_sent_if_rejected()
    {
        var scenario = Get(2);
        var approver = Rejects("Do not promise refunds; say a teammate will review.");
        var (lab, _) = ScenarioHarness.Build(scenario, approver);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var prompt = Assert.Single(approver.Prompts);
        Assert.Equal(PolicyStage.Judge, prompt.Decision.Stage);
        Assert.Equal(scenario.Drafts[0], prompt.Text);
        Assert.Equal(scenario.Ticket.CustomerAddress, prompt.Recipient);
        Assert.Equal(Channel.Email, prompt.Channel);
        Assert.Empty(lab.Outbox.Sent);
        Assert.Contains(lab.Audit.Entries, e => e.Event == "human-rejected" && e.Reason!.StartsWith("Do not promise", StringComparison.Ordinal));
    }

    [Fact]
    public async Task S2_refund_promise_is_sent_once_a_human_approves()
    {
        var scenario = Get(2);
        var (lab, _) = ScenarioHarness.Build(scenario, Approves());

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var sent = Assert.Single(lab.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
        Assert.Contains(lab.Audit.Entries, e => e.Event == "sent" && e.Actor == "reviewer@test");
    }

    [Fact]
    public async Task S3_ambiguous_reply_goes_to_a_human()
    {
        var scenario = Get(3);
        var approver = Rejects();
        var (lab, _) = ScenarioHarness.Build(scenario, approver);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Equal(PolicyStage.Judge, Assert.Single(approver.Prompts).Decision.Stage);
        Assert.Empty(lab.Outbox.Sent);
    }

    [Fact]
    public async Task S4_prompt_injection_is_never_auto_sent_even_if_the_judge_is_fooled()
    {
        var scenario = Get(4);
        var approver = Rejects();
        var (lab, model) = ScenarioHarness.Build(scenario, approver);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var prompt = Assert.Single(approver.Prompts);
        Assert.Equal(PolicyStage.Facts, prompt.Decision.Stage);
        // The attacker's address never becomes the recipient: it comes from the ticket.
        Assert.Equal(scenario.Ticket.CustomerAddress, prompt.Recipient);
        Assert.DoesNotContain(lab.Audit.Entries, e => e.Recipient?.Contains("attacker", StringComparison.Ordinal) == true);
        Assert.Empty(lab.Outbox.Sent);
        // The injected text did reach the model (it is the customer's message), so the defense is not "hide it".
        Assert.Contains(model.Requests[0], m => m.Text.Contains("Ignore your rules", StringComparison.Ordinal));
    }

    [Fact]
    public async Task S5_a_tampered_approval_cannot_change_the_text_that_is_sent()
    {
        var scenario = Get(5);
        var (lab, _) = ScenarioHarness.Build(scenario, Approves());

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.DoesNotContain(lab.Outbox.Sent, s => s.Text == ScenarioHarness.TamperedText);
        var sent = Assert.Single(lab.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
    }

    [Fact]
    public async Task S5_with_chat_client_binding_disabled_the_tool_approval_middleware_still_binds()
    {
        // Observed in Microsoft.Agents.AI 1.28.0: UseToolApproval (ToolApprovalAgent) also rebinds each
        // approval response to the request it surfaced, so turning off DisableApprovalResponseBinding
        // alone does not let the swapped text through.
        var scenario = Get(5);
        var (lab, _) = ScenarioHarness.Build(scenario, Approves(), disableApprovalResponseBinding: true);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        var sent = Assert.Single(lab.Outbox.Sent);
        Assert.Equal(scenario.Drafts[0], sent.Text);
    }

    [Fact]
    public void S5_the_send_tool_refuses_text_nobody_authorized()
    {
        // Second layer: even if a call reached the tool directly, unapproved text is not published.
        var (lab, _) = ScenarioHarness.Build(Get(5), Approves());

        var result = lab.Tools.SendReply("T-1002", ScenarioHarness.TamperedText);

        Assert.StartsWith("NOT SENT", result, StringComparison.Ordinal);
        Assert.Empty(lab.Outbox.Sent);
        Assert.Contains(lab.Audit.Entries, e => e.Event == "send-refused");
    }

    [Fact]
    public async Task S6_duplicate_is_blocked_without_a_human_and_not_resent()
    {
        var scenario = Get(6);
        var approver = Approves();
        var (lab, _) = ScenarioHarness.Build(scenario, approver);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Empty(approver.Prompts);
        Assert.Single(lab.Outbox.Sent); // only the earlier delivery
        Assert.Contains(lab.Audit.Entries, e => e is { Event: "policy-decision", Outcome: "Block", Stage: "Duplicate" });
    }

    [Fact]
    public async Task S7_judge_down_goes_to_a_human()
    {
        var scenario = Get(7);
        var approver = Rejects();
        var (lab, _) = ScenarioHarness.Build(scenario, approver);

        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        Assert.Equal(PolicyStage.Error, Assert.Single(approver.Prompts).Decision.Stage);
        Assert.Empty(lab.Outbox.Sent);
    }

    [Fact]
    public async Task Rejected_draft_is_rewritten_and_the_new_call_goes_through_the_whole_policy_again()
    {
        const string risky = "This was our mistake, we will refund you.";
        const string safe = "Our records show that order #A-1077 was delivered on November 7. A teammate will review your request.";
        var judge = new ScriptedJudge(text => text == risky
            ? JudgeSignal.EscalateFor(JudgeCategory.Commitment, "Promises a refund.")
            : JudgeSignal.Clear());
        var approver = Rejects("No refund promises.");
        var lab = Lab.Create(new ScriptedChatClient("T-1002", [risky, safe]), judge, approver);

        var result = await lab.Runner.RunAsync("T-1002", "Damaged package, refund me.");

        Assert.Single(approver.Prompts);
        Assert.Equal(2, judge.Calls);
        var sent = Assert.Single(lab.Outbox.Sent);
        Assert.Equal(safe, sent.Text);
        Assert.Equal(1, result.ApprovalRounds);
        Assert.Equal(
            ["policy-decision:NeedsHuman", "human-rejected:Rejected", "policy-decision:AutoSend", "sent:"],
            lab.Audit.Entries.Select(e => $"{e.Event}:{e.Outcome}"));
    }
}
