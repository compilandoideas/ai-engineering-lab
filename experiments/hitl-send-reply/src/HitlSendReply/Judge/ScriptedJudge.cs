using System.Diagnostics;
using HitlSendReply.Domain;

namespace HitlSendReply.Judge;

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

public sealed class FailingJudge(Exception error) : IReplyJudge
{
    public Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken) =>
        Task.FromException<JudgeSignal>(error);
}

/// <summary>Never answers; used to prove that a timeout escalates.</summary>
public sealed class HangingJudge : IReplyJudge
{
    public async Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }
}
