using HitlSendReply.Lab.Ui;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Scenarios;

/// <summary><c>scenario &lt;1-7|all&gt;</c>: runs the designed cases offline. The person at the terminal is the reviewer.</summary>
public static class ScenarioCommand
{
    public static async Task<int> RunAsync(string which, Language language)
    {
        var scenarios = ScenarioCatalog.For(language);
        var selected = which == "all" ? scenarios : scenarios.Where(s => s.Number.ToString() == which).ToList();
        if (selected.Count == 0)
        {
            Console.Error.WriteLine("Unknown scenario. Use 1-7 or all.");
            return 1;
        }

        ConsoleUi.Banner(ConsoleUi.T.Scenarios);
        foreach (var scenario in selected)
        {
            ConsoleUi.ScenarioHeader(scenario);

            var approver = new ConsoleApprover(Console.In, Environment.UserName);
            var (pipeline, _) = ScenarioHarness.Build(scenario, approver);
            var sentBefore = pipeline.Outbox.Sent.Count;
            await pipeline.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

            ConsoleUi.Trace(pipeline.Audit);
            ConsoleUi.Result(pipeline.Outbox, sentBefore);
        }

        return 0;
    }
}
