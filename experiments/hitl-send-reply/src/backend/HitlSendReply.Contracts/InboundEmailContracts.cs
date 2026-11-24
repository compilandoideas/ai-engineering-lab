using System.Text.Json.Serialization;

namespace HitlSendReply.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<InboundEmailStatus>))]
public enum InboundEmailStatus { Started, Unmatched, Duplicate }

/// <summary>
/// What an email provider's inbound webhook posts. EmailId makes the call idempotent: a provider
/// (or a queue) that delivers twice starts one run.
/// </summary>
public sealed record InboundEmailRequest(string EmailId, string From, string Subject, string Body, DateTimeOffset ReceivedAt);

/// <summary>Started (RunId set), Unmatched (no ticket for this email) or Duplicate (RunId of the first delivery).</summary>
public sealed record InboundEmailResult(InboundEmailStatus Status, string? RunId, string? TicketId, string? Reason);
