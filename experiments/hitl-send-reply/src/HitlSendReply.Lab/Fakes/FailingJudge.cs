using System.Diagnostics;
using HitlSendReply.Judge;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Fakes;

public sealed class FailingJudge(Exception error) : IReplyJudge
{
    public Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken) =>
        Task.FromException<JudgeSignal>(error);
}
