using System.Diagnostics;

namespace HitlSendReply.Infrastructure.Scripted;

public sealed class FailingJudge(Exception error) : IReplyJudge
{
    public Task<JudgeSignal> EvaluateAsync(string draft, Ticket ticket, CancellationToken cancellationToken) =>
        Task.FromException<JudgeSignal>(error);
}
