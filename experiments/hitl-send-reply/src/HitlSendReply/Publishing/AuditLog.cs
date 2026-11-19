using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HitlSendReply.Publishing;

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

/// <summary>Append-only record of every decision. Optionally mirrored to a JSONL file.</summary>
public sealed class AuditLog(TimeProvider clock, string? jsonlPath = null)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private readonly Lock _fileLock = new();

    public IReadOnlyList<AuditEntry> Entries => _entries.ToList();

    public AuditEntry Record(
        string @event, string? callId, string ticketId, string textHash,
        string? recipient = null, string? channel = null, string? outcome = null,
        string? stage = null, string? reason = null, string? actor = null)
    {
        var entry = new AuditEntry(clock.GetUtcNow(), @event, callId, ticketId, textHash,
            recipient, channel, outcome, stage, reason, actor);
        _entries.Enqueue(entry);

        if (jsonlPath is not null)
        {
            lock (_fileLock)
            {
                File.AppendAllText(jsonlPath, JsonSerializer.Serialize(entry, Json) + Environment.NewLine);
            }
        }

        return entry;
    }
}
