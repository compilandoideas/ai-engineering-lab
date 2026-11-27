namespace HitlSendReply.Domain.Tickets;

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
