using System.Text.Json;
using HitlSendReply;
using HitlSendReply.Agent;
using HitlSendReply.Cli;
using HitlSendReply.Evaluation;
using HitlSendReply.Judge;
using HitlSendReply.Publishing;

var command = args.FirstOrDefault()?.ToLowerInvariant();
return command switch
{
    "scenario" => await RunScenariosAsync(args.ElementAtOrDefault(1) ?? "all"),
    "live" => await RunLiveAsync(Option("--ticket") ?? "T-1002", Option("--message") ?? "The package arrived damaged. I want my money back."),
    "evaluate" => await EvaluateAsync(int.Parse(Option("--runs") ?? "3"), Option("--data") ?? Path.Combine(AppContext.BaseDirectory, "data", "drafts.jsonl")),
    _ => Help(),
};

string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static int Help()
{
    Console.WriteLine("""
        HITL send_reply experiment

          dotnet run -- scenario <1-7|all>     Designed scenarios, offline (scripted model and judge).
                                               You act as the reviewer in the terminal.
          dotnet run -- live [--ticket T-1002] [--message "..."]
                                               Real LLM agent + real judge (needs LAB_LLM_* variables).
          dotnet run -- evaluate [--runs 3]    Measure the real judge on data/drafts.jsonl.
        """);
    return 0;
}

static async Task<int> RunScenariosAsync(string which)
{
    var selected = which == "all" ? Scenarios.All : Scenarios.All.Where(s => s.Number.ToString() == which).ToList();
    if (selected.Count == 0)
        return Help();

    foreach (var scenario in selected)
    {
        Console.WriteLine($"\n═══ Scenario {scenario.Number}: {scenario.Name} ═══");
        Console.WriteLine($"Expected: {scenario.Expectation}");
        Console.WriteLine("(offline: the agent's draft and the judge's signal are scripted)");

        var approver = new ConsoleApprover(Console.In, Console.Out, Environment.UserName);
        var (lab, _) = ScenarioHarness.Build(scenario, approver);
        var result = await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        PrintAudit(lab.Audit);
        Console.WriteLine($"Sent: {lab.Outbox.Sent.Count} · Agent: {result.FinalText}");
    }

    return 0;
}

static async Task<int> RunLiveAsync(string ticketId, string message)
{
    if (LlmSettings.FromEnvironment() is not { } settings)
    {
        Console.Error.WriteLine(LlmSettings.MissingMessage);
        return 2;
    }

    using var model = settings.CreateChatClient();
    var auditPath = Path.Combine(Directory.GetCurrentDirectory(), "audit.jsonl");
    var lab = Lab.Create(model, new LlmReplyJudge(model), new ConsoleApprover(Console.In, Console.Out, Environment.UserName), auditPath: auditPath);

    Console.WriteLine($"Model: {settings.Model} · Ticket: {ticketId}\nCustomer: {message}");
    var result = await lab.Runner.RunAsync(ticketId, message);
    PrintAudit(lab.Audit);
    Console.WriteLine($"Sent: {lab.Outbox.Sent.Count} · Agent: {result.FinalText}\nAudit appended to {auditPath}");
    return 0;
}

static async Task<int> EvaluateAsync(int runs, string dataPath)
{
    if (LlmSettings.FromEnvironment() is not { } settings)
    {
        Console.Error.WriteLine(LlmSettings.MissingMessage);
        return 2;
    }

    using var model = settings.CreateChatClient();
    var judge = new LlmReplyJudge(model);
    var drafts = Evaluator.Load(dataPath);
    var reports = new List<RunReport>();

    for (var run = 1; run <= runs; run++)
    {
        var report = await Evaluator.RunOnceAsync(run, drafts, judge, TimeSpan.FromSeconds(60));
        reports.Add(report);
        Console.WriteLine($"Run {run}: correct {report.Correct}/{report.Total} · false negatives {report.FalseNegatives} · " +
                          $"false positives {report.FalsePositives} · judge misses {report.JudgeMisses}/{report.JudgeReached}");
        foreach (var miss in report.Items.Where(i => i.Actual != i.Expected))
            Console.WriteLine($"   {miss.Id} expected {miss.Expected}, got {miss.Actual} at {miss.Stage}: {miss.Note}");
    }

    var evaluation = new EvaluationReport(settings.Model, DateTimeOffset.UtcNow, reports);
    var dir = Path.Combine(Directory.GetCurrentDirectory(), "results");
    Directory.CreateDirectory(dir);
    var file = Path.Combine(dir, $"{evaluation.At:yyyyMMdd-HHmmss}-{string.Join("_", settings.Model.Split(Path.GetInvalidFileNameChars()))}.json");
    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(evaluation, Evaluator.Json));
    Console.WriteLine($"Results written to {file}");
    return 0;
}

static void PrintAudit(AuditLog audit)
{
    Console.WriteLine("Audit:");
    foreach (var e in audit.Entries)
        Console.WriteLine($"  {e.Event,-16} {e.TicketId} hash={(e.TextHash.Length >= 8 ? e.TextHash[..8] : e.TextHash)} " +
                          $"{e.Outcome} {e.Stage} by={e.Actor} {e.Reason}".TrimEnd());
}
