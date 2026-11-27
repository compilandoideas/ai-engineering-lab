
namespace HitlSendReply.Tests.Fakes;

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
