namespace HitlSendReply.Application.Abstractions;

/// <summary>An in-process reviewer: used by the designed scenarios and the tests, not by the HTTP host.</summary>
public interface IHumanApprover
{
    Task<HumanVerdict> ReviewAsync(ApprovalPrompt prompt, CancellationToken cancellationToken);
}
