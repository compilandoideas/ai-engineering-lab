namespace HitlSendReply.Application.Review;

/// <summary>The reviewer's edited text broke a deterministic rule: nothing was sent and the review stays pending.</summary>
public sealed class EditRefusedException(PolicyDecision decision) : InvalidOperationException(decision.Reason)
{
    public PolicyDecision Decision { get; } = decision;
}
