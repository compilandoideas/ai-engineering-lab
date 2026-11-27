using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace HitlSendReply.Infrastructure.InMemory;

/// <summary>In-memory audit log, optionally mirrored to a JSONL file.</summary>
public sealed class InMemoryAuditLog(TimeProvider clock, string? jsonlPath = null) : IAuditLog
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
