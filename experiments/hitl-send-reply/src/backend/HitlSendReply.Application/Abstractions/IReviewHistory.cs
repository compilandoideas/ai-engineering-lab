
namespace HitlSendReply.Application.Abstractions;

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
