using System.ComponentModel;
using System.Globalization;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;
using Microsoft.Extensions.AI;

namespace HitlSendReply.SendReply;

/// <summary>
/// Tools exposed to the support agent. send_reply takes only ticket id and text: the recipient
/// and channel are resolved from the ticket, so the model cannot redirect a reply.
/// </summary>
public sealed class SupportTools(ITicketStore tickets, IOutbox outbox, ApprovalLedger ledger, IAuditLog audit)
{
    [Description("Read the order facts of a ticket. Use them verbatim; do not invent facts.")]
    public string GetTicket([Description("Ticket id, for example T-1001")] string ticketId)
    {
        var ticket = tickets.Find(ticketId);
        if (ticket is null)
        {
            return $"Ticket {ticketId} not found.";
        }

        var o = ticket.Order;
        return string.Create(CultureInfo.InvariantCulture,
            $"Ticket {ticket.Id}. Reply language: {(ticket.Language == Language.Es ? "Spanish" : "English")}. Order #{o.Id}. Status: {o.Status}. Shipped on: {o.ShippedOn?.ToString("yyyy-MM-dd") ?? "not shipped"}. " +
            $"Estimated delivery: {o.EstimatedDelivery?.ToString("yyyy-MM-dd") ?? "unknown"}. Total: {o.Total:0.00} {o.Currency}.");
    }

    [Description("Send a reply to the customer of a ticket. Recipient and channel are taken from the ticket.")]
    public string SendReply(
        [Description("Ticket id, for example T-1001")] string ticketId,
        [Description("Exact text of the reply")] string text)
    {
        var key = ReplyKey.For(ticketId, text);
        var callId = FunctionInvokingChatClient.CurrentContext?.CallContent.CallId;
        var ticket = tickets.Find(ticketId);

        if (ticket is null)
        {
            audit.Record("send-refused", callId, ticketId, key.TextHash, reason: "Unknown ticket.");
            return "NOT SENT: unknown ticket.";
        }

        // Defense in depth: the framework already gated this call, but the tool itself only
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

    public IList<AITool> AsAITools() =>
    [
        AIFunctionFactory.Create(GetTicket, name: "get_ticket"),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendReply, name: SendReplyArguments.ToolName)),
    ];
}
