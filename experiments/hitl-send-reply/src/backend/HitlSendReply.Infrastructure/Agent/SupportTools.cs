using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;

namespace HitlSendReply.Infrastructure.Agent;

/// <summary>
/// Tools exposed to the support agent, as Microsoft Agent Framework functions. send_reply takes only ticket id and text: the recipient
/// and channel are resolved from the ticket, so the model cannot redirect a reply.
/// </summary>
public sealed class SupportTools(ITicketStore tickets, ReplySender sender)
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
        => Send(ticketId, text, FunctionInvokingChatClient.CurrentContext?.CallContent.CallId);

    /// <summary>The one send path, shared with a reviewer's edited text. See <see cref="ReplySender"/>.</summary>
    public string Send(string ticketId, string text, string? callId) => sender.Send(ticketId, text, callId);

    public IList<AITool> AsAITools() =>
    [
        AIFunctionFactory.Create(GetTicket, name: "get_ticket"),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendReply, name: SendReplyArguments.ToolName)),
    ];
}
