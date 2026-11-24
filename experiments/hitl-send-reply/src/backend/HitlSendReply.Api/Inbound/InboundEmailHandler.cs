using System.Collections.Concurrent;
using HitlSendReply.Api.Live;
using HitlSendReply.Api.Runs;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Contracts;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Inbound;

/// <summary>
/// An inbound email becomes a run on its ticket. Called by the webhook today; a queue consumer
/// would call the same method. Idempotent by EmailId: a second delivery returns the first run.
/// </summary>
public sealed class InboundEmailHandler(RunStore runs, LiveModels live)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<InboundEmailResult>>> _handled = new();

    public async Task<InboundEmailResult> HandleAsync(InboundEmailRequest email, CancellationToken cancellationToken)
    {
        // Once accepted, the run does not depend on the caller staying connected: a client timeout and
        // retry joins the same run instead of cancelling it.
        var first = new Lazy<Task<InboundEmailResult>>(() => StartAsync(email, CancellationToken.None));
        var handling = _handled.GetOrAdd(email.EmailId, first);
        try
        {
            var result = await handling.Value;
            return ReferenceEquals(handling, first) ? result : result with { Status = InboundEmailStatus.Duplicate };
        }
        catch
        {
            // A failed attempt did not start a run: forget it so the sender can retry.
            _handled.TryRemove(new KeyValuePair<string, Lazy<Task<InboundEmailResult>>>(email.EmailId, handling));
            throw;
        }
    }

    private async Task<InboundEmailResult> StartAsync(InboundEmailRequest email, CancellationToken cancellationToken)
    {
        if (TicketMatcher.Match(email.From, email.Subject) is not { } ticket)
        {
            return new InboundEmailResult(InboundEmailStatus.Unmatched, null, null, "No ticket for this sender or subject.");
        }

        var (pipeline, mode, scenario) = PipelineFor(ticket);
        var run = await pipeline.Runner.StartAsync(ticket.Id, email.Body, cancellationToken);
        var entry = new RunEntry(Guid.NewGuid().ToString("n"), mode, scenario, email.Body, pipeline, run);
        runs.Add(entry);
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
}
