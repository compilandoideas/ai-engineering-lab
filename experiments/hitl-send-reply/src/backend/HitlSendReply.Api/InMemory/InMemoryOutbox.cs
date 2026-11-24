using System.Collections.Concurrent;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.InMemory;

/// <summary>
/// Stand-in for the email gateway. Idempotent: the same (ticket, text hash) is delivered once.
/// </summary>
public sealed class InMemoryOutbox(TimeProvider clock) : IOutbox
{
    private readonly ConcurrentDictionary<ReplyKey, SentReply> _sent = new();

    public IReadOnlyCollection<SentReply> Sent => _sent.Values.OrderBy(s => s.SentAt).ToList();

    public bool HasSent(ReplyKey key) => _sent.ContainsKey(key);

    public bool TrySend(ReplyKey key, string recipient, Channel channel, string text) =>
        _sent.TryAdd(key, new SentReply(key, recipient, channel, text, clock.GetUtcNow()));
}
