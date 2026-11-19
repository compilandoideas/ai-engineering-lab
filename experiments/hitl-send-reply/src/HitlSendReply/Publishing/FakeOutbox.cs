using System.Collections.Concurrent;
using HitlSendReply.Domain;

namespace HitlSendReply.Publishing;

public sealed record SentReply(ReplyKey Key, string Recipient, Channel Channel, string Text, DateTimeOffset SentAt);

/// <summary>
/// Stand-in for the email gateway. Idempotent: the same (ticket, text hash) is delivered once.
/// </summary>
public sealed class FakeOutbox(TimeProvider clock)
{
    private readonly ConcurrentDictionary<ReplyKey, SentReply> _sent = new();

    public IReadOnlyCollection<SentReply> Sent => _sent.Values.OrderBy(s => s.SentAt).ToList();

    public bool HasSent(ReplyKey key) => _sent.ContainsKey(key);

    /// <returns>false when the key was already delivered (nothing is sent twice).</returns>
    public bool TrySend(ReplyKey key, string recipient, Channel channel, string text) =>
        _sent.TryAdd(key, new SentReply(key, recipient, channel, text, clock.GetUtcNow()));
}
