namespace HitlSendReply.Contracts;

public sealed record OrderDto(string Id, string Status, DateOnly? ShippedOn, DateOnly? EstimatedDelivery, decimal Total, string Currency);

public sealed record TicketDto(string Id, string CustomerAddress, string Channel, bool IsClosed, string Language, OrderDto Order);
