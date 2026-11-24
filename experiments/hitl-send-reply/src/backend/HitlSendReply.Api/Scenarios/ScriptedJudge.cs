using System.Diagnostics;
using HitlSendReply.Judge;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Scenarios;

/// <summary>
/// Deterministic judge for tests and offline demos. It returns what it is told to; it does not
/// pretend to understand text. Real judgment comes from <see cref="LlmReplyJudge"/>.
/// </summary>
public sealed class ScriptedJudge(Func<string, JudgeSignal> script) : IReplyJudge
{
    public int Calls { get; private set; }

    public Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(script(draft));
    }

    public static ScriptedJudge AlwaysClear() => new(_ => JudgeSignal.Clear());

    public static ScriptedJudge AlwaysEscalate(JudgeCategory category, string rationale) =>
        new(_ => JudgeSignal.EscalateFor(category, rationale));
}
