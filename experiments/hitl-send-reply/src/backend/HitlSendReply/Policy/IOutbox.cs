using HitlSendReply.Tickets;

namespace HitlSendReply.Policy;

/// <summary>A reply that was delivered.</summary>
public sealed record SentReply(ReplyKey Key, string Recipient, Channel Channel, string Text, DateTimeOffset SentAt);

/// <summary>Outbound channel to the customer. Must be idempotent per <see cref="ReplyKey"/>.</summary>
public interface IOutbox
{
    IReadOnlyCollection<SentReply> Sent { get; }

    bool HasSent(ReplyKey key);

    /// <returns>false when the key was already delivered (nothing is sent twice).</returns>
    bool TrySend(ReplyKey key, string recipient, Channel channel, string text);
}
