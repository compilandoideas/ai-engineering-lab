using HitlSendReply.Domain;

namespace HitlSendReply.Judge;

public enum JudgeCategory { None, Commitment, Fault, Legal, Ambiguous, Other }

/// <summary>
/// The judge's output is a signal, not an authorization. <see cref="Clear"/> only means
/// "I see no reason to escalate"; the policy still has to verify everything else.
/// </summary>
public sealed record JudgeSignal(bool Escalate, JudgeCategory Category, string Rationale)
{
    public static JudgeSignal Clear(string rationale = "No commitment, fault, legal topic or ambiguity found.") =>
        new(false, JudgeCategory.None, rationale);

    public static JudgeSignal EscalateFor(JudgeCategory category, string rationale) =>
        new(true, category, rationale);
}

/// <summary>
/// Reviews a draft reply. Sees only the draft and the order facts, never the customer's message,
/// so instructions injected by the customer cannot reach it.
/// </summary>
public interface IReplyJudge
{
    Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken);
}
