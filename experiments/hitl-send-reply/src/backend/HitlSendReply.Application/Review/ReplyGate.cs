using System.Collections.Concurrent;

namespace HitlSendReply.Application.Review;

/// <summary>
/// The policy as the agent sees it. Each proposed send is evaluated once (memoized by call id and
/// exact text), so the auto-approval rule and the orchestrator always see the same decision and the
/// judge is not called twice. An AutoSend authorizes that exact text in the ledger.
/// </summary>
public sealed class ReplyGate(PublishPolicy policy, ApprovalLedger ledger, IAuditLog audit, ITicketStore tickets)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PolicyDecision>>> _decisions = new();

    /// <param name="request">Null when the agent's call did not carry a ticket id and a text.</param>
    public Task<PolicyDecision> EvaluateAsync(string callId, ReplyRequest? request, CancellationToken cancellationToken = default) =>
        _decisions.GetOrAdd(CacheKey(callId, request), _ => new Lazy<Task<PolicyDecision>>(() => EvaluateCoreAsync(callId, request, cancellationToken))).Value;

    // Call id plus the exact arguments: a reused call id with different text is a different decision.
    private static string CacheKey(string callId, ReplyRequest? request) =>
        request is null ? $"{callId}|invalid" : $"{callId}|{request.Key.TicketId}|{request.Key.TextHash}";

    /// <summary>The deterministic rules on a reviewer's own text; null when it passes.</summary>
    public PolicyDecision? CheckHumanEdit(ReplyRequest request) => policy.CheckHumanEdit(request);

    private async Task<PolicyDecision> EvaluateCoreAsync(string callId, ReplyRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            var invalid = PolicyDecision.Block(PolicyStage.Ticket, "send_reply was called without ticketId and text.");
            audit.Record("policy-decision", callId, "?", "", outcome: invalid.Outcome.ToString(),
                stage: invalid.Stage.ToString(), reason: invalid.Reason, actor: "policy");
            return invalid;
        }

        var decision = await policy.EvaluateAsync(request, cancellationToken);
        var ticket = tickets.Find(request.TicketId);

        audit.Record("policy-decision", callId, request.TicketId, request.Key.TextHash,
            ticket?.CustomerAddress, ticket?.Channel.ToString(), decision.Outcome.ToString(),
            decision.Stage.ToString(), decision.Reason, actor: "policy");

        if (decision.Outcome == PolicyOutcome.AutoSend)
        {
            ledger.Authorize(request.Key, "policy");
        }

        return decision;
    }
}
