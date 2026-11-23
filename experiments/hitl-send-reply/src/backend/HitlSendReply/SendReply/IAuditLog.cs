using HitlSendReply.Tickets;

namespace HitlSendReply.SendReply;

public sealed record AuditEntry(
    DateTimeOffset At,
    string Event,
    string? CallId,
    string TicketId,
    string TextHash,
    string? Recipient,
    string? Channel,
    string? Outcome,
    string? Stage,
    string? Reason,
    string? Actor);

/// <summary>Append-only record of every decision.</summary>
public interface IAuditLog
{
    IReadOnlyList<AuditEntry> Entries { get; }

    AuditEntry Record(
        string @event, string? callId, string ticketId, string textHash,
        string? recipient = null, string? channel = null, string? outcome = null,
        string? stage = null, string? reason = null, string? actor = null);
}
