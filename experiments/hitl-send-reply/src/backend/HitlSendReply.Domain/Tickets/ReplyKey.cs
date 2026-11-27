using System.Security.Cryptography;
using System.Text;

namespace HitlSendReply.Domain.Tickets;

/// <summary>
/// Idempotency key: ticket id + SHA-256 of the exact text. Also used to bind an approval
/// to the text a human (or the policy) actually saw.
/// </summary>
public readonly record struct ReplyKey(string TicketId, string TextHash)
{
    public static ReplyKey For(string ticketId, string text) =>
        new(ticketId, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));

    public override string ToString() => $"{TicketId}:{TextHash[..12]}";
}
