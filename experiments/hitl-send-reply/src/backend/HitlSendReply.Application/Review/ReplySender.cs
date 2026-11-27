namespace HitlSendReply.Application.Review;

/// <summary>
/// The one send path: the agent's send_reply and a reviewer's edited text both go through it, so
/// both need the exact text authorized in the ledger. Recipient and channel come from the ticket.
/// </summary>
public sealed class ReplySender(ITicketStore tickets, IOutbox outbox, ApprovalLedger ledger, IAuditLog audit)
{
    public string Send(string ticketId, string text, string? callId)
    {
        var key = ReplyKey.For(ticketId, text);
        var ticket = tickets.Find(ticketId);

        if (ticket is null)
        {
            audit.Record("send-refused", callId, ticketId, key.TextHash, reason: "Unknown ticket.");
            return "NOT SENT: unknown ticket.";
        }

        // Defense in depth: the agent framework already gated this call, but the send itself only
        // publishes text that the policy or a named human authorized, byte for byte.
        if (!ledger.TryGetAuthorizer(key, out var authorizedBy))
        {
            audit.Record("send-refused", callId, ticketId, key.TextHash, ticket.CustomerAddress, ticket.Channel.ToString(),
                reason: "This exact text was not authorized.");
            return "NOT SENT: this exact text was not authorized.";
        }

        if (!outbox.TrySend(key, ticket.CustomerAddress, ticket.Channel, text))
        {
            audit.Record("send-duplicate", callId, ticketId, key.TextHash, ticket.CustomerAddress, ticket.Channel.ToString(),
                reason: "Idempotency key already used.", actor: authorizedBy);
            return "NOT SENT: this reply was already sent.";
        }

        audit.Record("sent", callId, ticketId, key.TextHash, ticket.CustomerAddress, ticket.Channel.ToString(), actor: authorizedBy);
        return $"SENT to the customer via {ticket.Channel}.";
    }
}
