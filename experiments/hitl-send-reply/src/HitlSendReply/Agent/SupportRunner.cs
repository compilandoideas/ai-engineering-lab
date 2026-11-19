using HitlSendReply.Domain;
using HitlSendReply.Policy;
using HitlSendReply.Publishing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HitlSendReply.Agent;

public sealed record RunResult(string FinalText, int ApprovalRounds);

/// <summary>
/// Runs the agent for one ticket and resolves every approval request the framework surfaces:
/// Block → rejected without a human; NeedsHuman (or anything not auto-approved) → reviewer.
/// </summary>
public sealed class SupportRunner(
    AIAgent agent,
    ReplyGate gate,
    IHumanApprover human,
    ApprovalLedger ledger,
    AuditLog audit,
    ITicketStore tickets,
    int maxApprovalRounds = 4)
{
    /// <summary>
    /// How approval responses are built. Defaults to the framework's CreateResponse. Tests replace
    /// it to simulate a tampered response (scenario 5).
    /// </summary>
    public Func<ToolApprovalRequestContent, bool, string, AIContent> CreateResponse { get; init; } =
        static (request, approved, reason) => request.CreateResponse(approved, reason);

    public async Task<RunResult> RunAsync(string ticketId, string customerMessage, CancellationToken cancellationToken = default)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);
        var input = new ChatMessage(ChatRole.User, $"""
            Ticket {ticketId}.
            <customer_message>
            {customerMessage}
            </customer_message>
            """);

        var response = await agent.RunAsync([input], session, cancellationToken: cancellationToken);
        var rounds = 0;

        while (rounds < maxApprovalRounds)
        {
            var pending = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
            if (pending.Count == 0)
                break;

            rounds++;
            List<AIContent> answers = [];
            foreach (var request in pending)
                answers.Add(await ResolveAsync(request, cancellationToken));

            response = await agent.RunAsync([new ChatMessage(ChatRole.User, answers)], session, cancellationToken: cancellationToken);
        }

        return new RunResult(response.Text, rounds);
    }

    private async Task<AIContent> ResolveAsync(ToolApprovalRequestContent request, CancellationToken cancellationToken)
    {
        if (request.ToolCall is not FunctionCallContent call || ReplyArguments.TryRead(call) is not { } reply)
            return CreateResponse(request, false, "Unsupported tool call.");

        var decision = await gate.EvaluateAsync(call, cancellationToken);
        if (decision.Outcome == PolicyOutcome.Block)
            return CreateResponse(request, false, $"Blocked by policy: {decision.Reason}");

        // NeedsHuman, or an AutoSend that surfaced anyway (auto-approval cap reached): a person decides.
        var ticket = tickets.Find(reply.TicketId)!;
        var verdict = await human.ReviewAsync(
            new ApprovalPrompt(call.CallId, reply.TicketId, ticket.CustomerAddress, ticket.Channel, reply.Text, decision),
            cancellationToken);

        audit.Record(verdict.Approved ? "human-approved" : "human-rejected", call.CallId, reply.TicketId, reply.Key.TextHash,
            ticket.CustomerAddress, ticket.Channel.ToString(), verdict.Approved ? "Approved" : "Rejected",
            decision.Stage.ToString(), verdict.Reason, verdict.Reviewer);

        if (verdict.Approved)
            ledger.Authorize(reply.Key, verdict.Reviewer);

        return CreateResponse(request, verdict.Approved, verdict.Reason);
    }
}
