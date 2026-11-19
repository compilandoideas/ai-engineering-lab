using HitlSendReply.Domain;
using HitlSendReply.Policy;

namespace HitlSendReply.Agent;

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

/// <summary>Programmed reviewer for tests. Records every prompt it was shown.</summary>
public sealed class ScriptedApprover(Func<ApprovalPrompt, HumanVerdict> decide) : IHumanApprover
{
    private readonly List<ApprovalPrompt> _prompts = [];

    public IReadOnlyList<ApprovalPrompt> Prompts => _prompts;

    public Task<HumanVerdict> ReviewAsync(ApprovalPrompt prompt, CancellationToken cancellationToken)
    {
        _prompts.Add(prompt);
        return Task.FromResult(decide(prompt));
    }
}
