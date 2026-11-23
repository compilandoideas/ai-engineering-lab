using HitlSendReply.Policy;
using HitlSendReply.Tickets;

namespace HitlSendReply.SendReply;

/// <summary>What the reviewer sees: the exact text, where it goes, and why it was escalated.</summary>
public sealed record ApprovalPrompt(string CallId, string TicketId, string Recipient, Channel Channel, string Text, PolicyDecision Decision);

public sealed record HumanVerdict(bool Approved, string Reason, string Reviewer)
{
    public static HumanVerdict Approve(string reviewer) => new(true, "Approved by reviewer.", reviewer);

    public static HumanVerdict Reject(string reviewer, string reason) => new(false, reason, reviewer);
}

public interface IHumanApprover
{
    Task<HumanVerdict> ReviewAsync(ApprovalPrompt prompt, CancellationToken cancellationToken);
}
