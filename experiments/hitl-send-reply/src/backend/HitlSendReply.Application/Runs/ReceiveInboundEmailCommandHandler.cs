using Microsoft.Extensions.Logging;
using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

/// <summary>
/// An inbound email becomes a run on its ticket. Sent by the webhook today; a queue consumer
/// would send the same command. It answers as soon as the run is registered: the agent drafts in
/// the background, so the sender never waits for the model. Idempotent by EmailId.
/// </summary>
internal sealed partial class ReceiveInboundEmailCommandHandler(
    InboundEmailLog log,
    RunStore runs,
    IRunSessionFactory sessions,
    TicketMatcher matcher,
    ILogger<ReceiveInboundEmailCommandHandler> logger)
    : ICommandHandler<ReceiveInboundEmailCommand, InboundEmailResult>
{
    public Task<Result<InboundEmailResult>> Handle(ReceiveInboundEmailCommand email, CancellationToken cancellationToken)
    {
        lock (log.Gate)
        {
            if (log.TryGet(email.EmailId, out var first))
            {
                return Task.FromResult(Result.Success(first with { Status = InboundEmailStatus.Duplicate }));
            }

            var result = Register(email);
            log.Add(email.EmailId, result);
            return Task.FromResult(Result.Success(result));
        }
    }

    private InboundEmailResult Register(ReceiveInboundEmailCommand email)
    {
        if (matcher.Match(email.From, email.Subject) is not { } ticket)
        {
            return new InboundEmailResult(InboundEmailStatus.Unmatched, null, null, "No ticket for this sender or subject.");
        }

        var session = sessions.Create(ticket);
        var entry = new RunEntry(Guid.NewGuid().ToString("n"), ticket.Id, email.Body, session);
        runs.Add(entry);
        entry.StartInBackground(
            () => session.Agent.StartAsync(ticket.Id, email.Body, CancellationToken.None),
            ex => LogStartFailed(entry.Id, ticket.Id, ex));
        return new InboundEmailResult(InboundEmailStatus.Started, entry.Id, ticket.Id, null);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} for ticket {TicketId} failed to start")]
    private partial void LogStartFailed(string runId, string ticketId, Exception exception);
}
