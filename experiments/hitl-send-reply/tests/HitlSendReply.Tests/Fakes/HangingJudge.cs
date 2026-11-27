using System.Diagnostics;

namespace HitlSendReply.Tests.Fakes;

/// <summary>Never answers; used to prove that a timeout escalates.</summary>
public sealed class HangingJudge : IReplyJudge
{
    public async Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }
}
