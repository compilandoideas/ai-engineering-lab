using HitlSendReply.Tickets;

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
/// so instructions injected by the customer cannot reach it. The policy depends on this interface only; LlmReplyJudge and RubricReplyJudge implement it.
/// <para>
/// Extension point for more detection layers. A cheap deterministic screen (lexicon/rules), a
/// rubric judge or a second model can be chained here behind one composite <see cref="IReplyJudge"/>
/// with an OR rule: escalate if any layer escalates, and treat a layer that fails or times out as
/// an escalation. No layer can turn another layer's escalation into a send. See README,
/// "Detection layers".
/// </para>
/// </summary>
public interface IReplyJudge
{
    Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken);
}
