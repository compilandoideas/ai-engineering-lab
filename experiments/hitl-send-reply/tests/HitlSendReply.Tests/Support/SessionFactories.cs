using HitlSendReply.Contracts;

namespace HitlSendReply.Tests.Support;

/// <summary>Builds each run's session with the given function, so a test decides who plays the agent.</summary>
public sealed class DelegateSessionFactory(Func<Ticket, RunSession> create) : IRunSessionFactory
{
    public RunSession Create(Ticket ticket) => create(ticket);
}

/// <summary>
/// The next run started over HTTP plays this designed scenario (scripted model and judge), so the
/// seven cases can be driven end to end through the inbound email webhook.
/// </summary>
public sealed class ScenarioSessionFactory : IRunSessionFactory
{
    public Scenario? Next { get; set; }

    public RunSession Create(Ticket ticket)
    {
        var scenario = Next ?? throw new InvalidOperationException("Set Next before sending the email.");
        var (pipeline, _) = ScenarioHarness.Build(scenario, approver: null);
        return pipeline.Session(RunMode.Scenario, scenario.Number);
    }
}
