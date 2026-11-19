namespace HitlSendReply.Domain;

public enum Channel { Email, Chat }

public enum OrderStatus { Processing, Shipped, Delivered, Cancelled }

/// <summary>Language of the customer and of the replies on a ticket.</summary>
public enum Language { En, Es }

/// <summary>
/// A support ticket as the system of record knows it. Recipient and channel come from here,
/// never from the model.
/// </summary>
public sealed record Ticket(
    string Id,
    string CustomerAddress,
    Channel Channel,
    bool IsClosed,
    Order Order,
    Language Language = Language.En);

/// <summary>The order facts a reply is allowed to state. Only these can be verified.</summary>
public sealed record Order(
    string Id,
    OrderStatus Status,
    DateOnly? ShippedOn,
    DateOnly? EstimatedDelivery,
    decimal Total,
    string Currency);

/// <summary>What the agent asked to send: ticket id and text. Nothing else is trusted from the model.</summary>
public sealed record ReplyRequest(string TicketId, string Text)
{
    public ReplyKey Key => ReplyKey.For(TicketId, Text);
}
