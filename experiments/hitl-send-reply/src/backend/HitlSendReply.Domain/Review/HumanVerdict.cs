
namespace HitlSendReply.Domain.Review;

/// <summary>What the reviewer sees: the exact text, where it goes, and why it was escalated.</summary>
public sealed record ApprovalPrompt(string CallId, string TicketId, string Recipient, Channel Channel, string Text, PolicyDecision Decision);

/// <summary>
/// A person's verdict. With EditedText the person approves their own version instead of the
/// agent's: it passes the deterministic rules again (not the judge) and the person answers for it.
/// </summary>
public sealed record HumanVerdict(bool Approved, string Reason, string Reviewer, string? EditedText = null)
{
    public static HumanVerdict Approve(string reviewer) => new(true, "Approved by reviewer.", reviewer);

    public static HumanVerdict Edit(string reviewer, string text) => new(true, "Edited and approved by reviewer.", reviewer, text);

    public static HumanVerdict Reject(string reviewer, string reason) => new(false, reason, reviewer);
}
