using System.Collections.Concurrent;
using HitlSendReply.Domain;

namespace HitlSendReply.Publishing;

/// <summary>
/// Who authorized which exact text. The send tool refuses anything that is not here, so even
/// a call that slips past the framework's approval step cannot publish unreviewed text.
/// </summary>
public sealed class ApprovalLedger
{
    private readonly ConcurrentDictionary<ReplyKey, string> _authorizations = new();

    public void Authorize(ReplyKey key, string authorizedBy) => _authorizations[key] = authorizedBy;

    public bool TryGetAuthorizer(ReplyKey key, out string authorizedBy) =>
        _authorizations.TryGetValue(key, out authorizedBy!);
}
