using HitlSendReply.Domain;

namespace HitlSendReply.Policy;

// Ports the policy and the agent adapter depend on. Implementations live outside Domain/Policy:
// in-memory fakes in Publishing/, the LLM judge in Judge/. Nothing here knows about MAF or an LLM.

public sealed record SentReply(ReplyKey Key, string Recipient, Channel Channel, string Text, DateTimeOffset SentAt);

/// <summary>Outbound channel to the customer. Must be idempotent per <see cref="ReplyKey"/>.</summary>
public interface IOutbox
{
    IReadOnlyCollection<SentReply> Sent { get; }

    bool HasSent(ReplyKey key);

    /// <returns>false when the key was already delivered (nothing is sent twice).</returns>
    bool TrySend(ReplyKey key, string recipient, Channel channel, string text);
}

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

/// <summary>
/// What humans rejected. A rejection changes the risk of the whole ticket: the agent may rewrite
/// toward the reviewer's reason and still be wrong, so the next draft is not auto-sent.
/// </summary>
public interface IReviewHistory
{
    void RecordRejection(ReplyKey key, string reason);

    bool WasRejected(ReplyKey key);

    bool HasRejection(string ticketId);
}
