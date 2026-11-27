namespace HitlSendReply.Domain.Tickets;

public enum OrderStatus { Processing, Shipped, Delivered, Cancelled }

/// <summary>The order facts a reply is allowed to state. Only these can be verified.</summary>
public sealed record Order(
    string Id,
    OrderStatus Status,
    DateOnly? ShippedOn,
    DateOnly? EstimatedDelivery,
    decimal Total,
    string Currency);
