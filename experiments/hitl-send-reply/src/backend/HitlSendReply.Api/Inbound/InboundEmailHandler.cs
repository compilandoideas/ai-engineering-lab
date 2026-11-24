using System.Collections.Concurrent;
using HitlSendReply.Api.Live;
using HitlSendReply.Api.Runs;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Contracts;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Inbound;

/// <summary>
/// An inbound email becomes a run on its ticket. Called by the webhook today; a queue consumer
/// would call the same method. It answers as soon as the run is registered: the agent drafts in
/// the background, so the sender never waits for the model. Idempotent by EmailId.
/// </summary>
public sealed partial class InboundEmailHandler(RunStore runs, LiveModels live, ILogger<InboundEmailHandler> logger)
{
    private readonly ConcurrentDictionary<string, InboundEmailResult> _handled = new();
    private readonly Lock _gate = new();

    /// <summary>For tests: how a ticket gets its pipeline. Defaults to the live model or the designed scenario.</summary>
    internal Func<Ticket, (Pipeline Pipeline, RunMode Mode, int? Scenario)>? PipelineFactory { get; init; }

    public InboundEmailResult Handle(InboundEmailRequest email)
    {
        // Registering is quick and synchronous, so one lock makes "first delivery wins" exact.
        lock (_gate)
        {
            if (_handled.TryGetValue(email.EmailId, out var first))
            {
                return first with { Status = InboundEmailStatus.Duplicate };
            }

            var result = Register(email);
            _handled[email.EmailId] = result;
            return result;
        }
    }

    private InboundEmailResult Register(InboundEmailRequest email)
    {
        if (TicketMatcher.Match(email.From, email.Subject) is not { } ticket)
        {
            return new InboundEmailResult(InboundEmailStatus.Unmatched, null, null, "No ticket for this sender or subject.");
        }

        var (pipeline, mode, scenario) = (PipelineFactory ?? PipelineFor)(ticket);
        var entry = new RunEntry(Guid.NewGuid().ToString("n"), mode, scenario, ticket.Id, email.Body, pipeline);
        runs.Add(entry);
        entry.StartInBackground(
            () => pipeline.Runner.StartAsync(ticket.Id, email.Body, CancellationToken.None),
            ex => LogStartFailed(entry.Id, ticket.Id, ex));
        return new InboundEmailResult(InboundEmailStatus.Started, entry.Id, ticket.Id, null);
    }

    // With a model configured the real agent answers. Without one, the designed scenario for that
    // ticket plays the agent (scripted drafts and judge), so the demo works with no key.
    private (Pipeline Pipeline, RunMode Mode, int? Scenario) PipelineFor(Ticket ticket)
    {
        if (live.IsConfigured)
        {
            return (Pipeline.Create(live.Agent, live.CreateJudge(), human: null, ticket.Language), RunMode.Live, null);
        }

        var scenario = ScenarioCatalog.For(ticket.Language)
            .FirstOrDefault(s => s.Ticket.Id == ticket.Id && s.Number is not (5 or 6))
            ?? ScenarioCatalog.For(ticket.Language).First(s => s.Number == 3) with { Ticket = ticket };
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver: null);
        return (pipeline, RunMode.Scenario, scenario.Number);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} for ticket {TicketId} failed to start")]
    private partial void LogStartFailed(string runId, string ticketId, Exception exception);
}
