using HitlSendReply.Domain;

namespace HitlSendReply.Policy;

public sealed record PublishPolicyOptions(string AgentName, TimeSpan JudgeTimeout);

/// <summary>
/// Decides what happens to a send_reply call. Order: ticket → permissions → duplicate →
/// prior rejection → facts → judge. Deterministic checks can block or escalate; the judge can only escalate.
/// AutoSend requires every stage to pass. Any exception ends in NeedsHuman.
/// </summary>
public sealed class PublishPolicy(
    ITicketStore tickets,
    AgentPermissions permissions,
    IOutbox outbox,
    IReplyJudge judge,
    PublishPolicyOptions options,
    IReviewHistory? reviews = null)
{
    public async Task<PolicyDecision> EvaluateAsync(ReplyRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var ticket = tickets.Find(request.TicketId);
            if (ticket is null)
                return PolicyDecision.Block(PolicyStage.Ticket, $"Ticket '{request.TicketId}' does not exist.");

            if (ticket.IsClosed)
                return PolicyDecision.Block(PolicyStage.Ticket, $"Ticket {ticket.Id} is closed.");

            if (!permissions.CanReply(options.AgentName, ticket.Channel))
                return PolicyDecision.Block(PolicyStage.Permissions,
                    $"Agent '{options.AgentName}' is not allowed to reply on {ticket.Channel}.");

            if (outbox.HasSent(request.Key))
                return PolicyDecision.Block(PolicyStage.Duplicate, "This exact text was already sent on this ticket.");

            if (reviews?.WasRejected(request.Key) == true)
                return PolicyDecision.Block(PolicyStage.PriorRejection, "A reviewer already rejected this exact text.");

            // Observed with a real model: after a rejection the agent sometimes rewrote toward the
            // reason and the rewrite auto-sent, so no human saw the final text. Not any more.
            if (reviews?.HasRejection(ticket.Id) == true)
                return PolicyDecision.Human(PolicyStage.PriorRejection,
                    "A reviewer rejected an earlier draft on this ticket; the rewrite needs a human too.");

            if (string.IsNullOrWhiteSpace(request.Text))
                return PolicyDecision.Block(PolicyStage.Facts, "Empty reply.");

            var facts = FactChecker.Check(request.Text, ticket.Order, ticket.Language);
            if (!facts.Verified)
                return PolicyDecision.Human(PolicyStage.Facts, string.Join(" ", facts.Issues));

            var signal = await EvaluateWithTimeoutAsync(request.Text, ticket, cancellationToken);
            return signal.Escalate
                ? PolicyDecision.Human(PolicyStage.Judge, $"Judge escalated ({signal.Category}): {signal.Rationale}")
                : PolicyDecision.Auto("Permissions, duplicate and facts verified; judge raised nothing.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PolicyDecision.Human(PolicyStage.Error, $"Judge timed out after {options.JudgeTimeout.TotalSeconds:0.##}s.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PolicyDecision.Human(PolicyStage.Error, $"Policy could not complete: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<JudgeSignal> EvaluateWithTimeoutAsync(string text, Ticket ticket, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.JudgeTimeout);
        return await judge.EvaluateAsync(text, ticket, cts.Token).WaitAsync(cts.Token);
    }
}
