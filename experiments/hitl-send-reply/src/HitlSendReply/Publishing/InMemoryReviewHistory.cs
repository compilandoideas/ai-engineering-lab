using System.Collections.Concurrent;
using HitlSendReply.Domain;
using HitlSendReply.Policy;

namespace HitlSendReply.Publishing;

public sealed class InMemoryReviewHistory : IReviewHistory
{
    private readonly ConcurrentDictionary<ReplyKey, string> _rejected = new();

    public void RecordRejection(ReplyKey key, string reason) => _rejected[key] = reason;

    public bool WasRejected(ReplyKey key) => _rejected.ContainsKey(key);

    public bool HasRejection(string ticketId) =>
        _rejected.Keys.Any(k => string.Equals(k.TicketId, ticketId, StringComparison.OrdinalIgnoreCase));
}
