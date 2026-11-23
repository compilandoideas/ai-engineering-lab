using System.Collections.Concurrent;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HitlSendReply.SendReply;

/// <summary>
/// Bridges the framework's approval pipeline and the application policy. Each tool call is
/// evaluated once (memoized by call id), so the auto-approval rule and the orchestrator always
/// see the same decision and the judge is not called twice.
/// </summary>
public sealed class ReplyGate(PublishPolicy policy, ApprovalLedger ledger, IAuditLog audit, ITicketStore tickets)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PolicyDecision>>> _decisions = new();

    public Task<PolicyDecision> EvaluateAsync(FunctionCallContent call, CancellationToken cancellationToken = default) =>
        _decisions.GetOrAdd(CacheKey(call), _ => new Lazy<Task<PolicyDecision>>(() => EvaluateCoreAsync(call, cancellationToken))).Value;

    // Call id plus the exact arguments: a reused call id with different text is a different decision.
    private static string CacheKey(FunctionCallContent call) =>
        SendReplyArguments.TryRead(call) is { } r ? $"{call.CallId}|{r.Key.TicketId}|{r.Key.TextHash}" : $"{call.CallId}|invalid";

    /// <summary>Auto-approval rule: returns true only when the policy says AutoSend.</summary>
    public async ValueTask<bool> AutoApproveAsync(ToolAutoApprovalRuleContext context)
    {
        if (context.FunctionCallContent.Name != SendReplyArguments.ToolName)
        {
            return false;
        }

        var decision = await EvaluateAsync(context.FunctionCallContent);
        return decision.Outcome == PolicyOutcome.AutoSend;
    }

    private async Task<PolicyDecision> EvaluateCoreAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        var request = SendReplyArguments.TryRead(call);
        if (request is null)
        {
            var invalid = PolicyDecision.Block(PolicyStage.Ticket, "send_reply was called without ticketId and text.");
            audit.Record("policy-decision", call.CallId, "?", "", outcome: invalid.Outcome.ToString(),
                stage: invalid.Stage.ToString(), reason: invalid.Reason, actor: "policy");
            return invalid;
        }

        var decision = await policy.EvaluateAsync(request, cancellationToken);
        var ticket = tickets.Find(request.TicketId);

        audit.Record("policy-decision", call.CallId, request.TicketId, request.Key.TextHash,
            ticket?.CustomerAddress, ticket?.Channel.ToString(), decision.Outcome.ToString(),
            decision.Stage.ToString(), decision.Reason, actor: "policy");

        if (decision.Outcome == PolicyOutcome.AutoSend)
        {
            ledger.Authorize(request.Key, "policy");
        }

        return decision;
    }
}
