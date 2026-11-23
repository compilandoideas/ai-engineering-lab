using System.Diagnostics;
using HitlSendReply.Judge;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Fakes;

/// <summary>Never answers; used to prove that a timeout escalates.</summary>
public sealed class HangingJudge : IReplyJudge
{
    public async Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    }
}
