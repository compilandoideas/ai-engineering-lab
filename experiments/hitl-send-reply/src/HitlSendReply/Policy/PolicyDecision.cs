namespace HitlSendReply.Policy;

public enum PolicyOutcome
{
    /// <summary>Rejected by the application. No human is asked.</summary>
    Block,

    /// <summary>A human must see the exact text, recipient and channel.</summary>
    NeedsHuman,

    /// <summary>Every verifiable condition passed and the judge raised nothing.</summary>
    AutoSend,
}

public enum PolicyStage { Ticket, Permissions, Duplicate, Facts, Judge, Error }

public sealed record PolicyDecision(PolicyOutcome Outcome, PolicyStage Stage, string Reason)
{
    public static PolicyDecision Block(PolicyStage stage, string reason) => new(PolicyOutcome.Block, stage, reason);

    public static PolicyDecision Human(PolicyStage stage, string reason) => new(PolicyOutcome.NeedsHuman, stage, reason);

    public static PolicyDecision Auto(string reason) => new(PolicyOutcome.AutoSend, PolicyStage.Judge, reason);
}
