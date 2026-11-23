namespace HitlSendReply.Tickets;

/// <summary>What the agent asked to send: ticket id and text. Nothing else is trusted from the model.</summary>
public sealed record ReplyRequest(string TicketId, string Text)
{
    public ReplyKey Key => ReplyKey.For(TicketId, Text);
}
