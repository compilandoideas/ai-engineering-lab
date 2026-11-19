using System.Text.Json;
using HitlSendReply;
using HitlSendReply.Agent;
using HitlSendReply.Cli;
using HitlSendReply.Domain;
using HitlSendReply.Evaluation;
using HitlSendReply.Judge;
using HitlSendReply.Publishing;
using Spectre.Console;

ConsoleUi.Configure();
ConsoleUi.T = UiText.For(Lang());
var command = args.FirstOrDefault()?.ToLowerInvariant();
return command switch
{
    "scenario" => await RunScenariosAsync(args.ElementAtOrDefault(1) is { } w && !w.StartsWith("--", StringComparison.Ordinal) ? w : "all", Lang()),
    "live" => await RunLiveAsync(Option("--ticket") ?? "T-1002", Option("--message") ?? DefaultMessage(Lang()), Lang()),
    "evaluate" => await EvaluateAsync(int.Parse(Option("--runs") ?? "3"), Lang()),
    _ => Help(),
};

Language Lang() => (Option("--lang") ?? "en").ToLowerInvariant() == "es" ? Language.Es : Language.En;

static string DefaultMessage(Language language) => language == Language.Es
    ? "El paquete llegó dañado. Quiero mi dinero de vuelta."
    : "The package arrived damaged. I want my money back.";

string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static int Help()
{
    Console.WriteLine("""
        HITL send_reply experiment

          dotnet run -- scenario <1-7|all> [--lang en|es]
              Designed scenarios, offline (scripted model and judge). You are the reviewer.
          dotnet run -- live [--lang en|es] [--ticket T-1002] [--message "..."]
              Real LLM agent + real judge (needs OPENAI_API_KEY or LAB_LLM_* variables).
          dotnet run -- evaluate [--lang en|es] [--runs 3]
              Measure the real judge on data/drafts.<lang>.jsonl.
        """);
    return 0;
}

static async Task<int> RunScenariosAsync(string which, Language language)
{
    var scenarios = Scenarios.For(language);
    var selected = which == "all" ? scenarios : scenarios.Where(s => s.Number.ToString() == which).ToList();
    if (selected.Count == 0)
        return Help();

    ConsoleUi.Banner(ConsoleUi.T.Scenarios);
    foreach (var scenario in selected)
    {
        ConsoleUi.ScenarioHeader(scenario);

        var approver = new ConsoleApprover(Console.In, Environment.UserName);
        var (lab, _) = ScenarioHarness.Build(scenario, approver);
        var sentBefore = lab.Outbox.Sent.Count;
        await lab.Runner.RunAsync(scenario.Ticket.Id, scenario.CustomerMessage);

        ConsoleUi.Trace(lab.Audit);
        ConsoleUi.Result(lab.Outbox, sentBefore);
    }

    return 0;
}

static async Task<int> RunLiveAsync(string ticketId, string message, Language language)
{
    if (LlmSettings.FromEnvironment() is not { } settings)
    {
        Console.Error.WriteLine(LlmSettings.MissingMessage);
        return 2;
    }

    using var agentModel = settings.CreateChatClient(settings.AgentModel);
    using var judgeModel = settings.CreateChatClient(settings.JudgeModel);
    var auditPath = Path.Combine(Directory.GetCurrentDirectory(), "audit.jsonl");
    var lab = Lab.Create(agentModel, new LlmReplyJudge(judgeModel), new ConsoleApprover(Console.In, Environment.UserName), language, auditPath: auditPath);

    ConsoleUi.Banner(ConsoleUi.T.LiveModel);
    ConsoleUi.LiveHeader($"agent {settings.AgentModel} · judge {settings.JudgeModel}", ticketId, message);
    var result = await lab.Runner.RunAsync(ticketId, message);
    ConsoleUi.Trace(lab.Audit);
    ConsoleUi.Result(lab.Outbox, 0);
    AnsiConsole.MarkupLine($"[grey]Agent:[/] {Markup.Escape(result.FinalText)}\n[grey]Audit appended to {Markup.Escape(auditPath)}[/]");
    return 0;
}

static async Task<int> EvaluateAsync(int runs, Language language)
{
    if (LlmSettings.FromEnvironment() is not { } settings)
    {
        Console.Error.WriteLine(LlmSettings.MissingMessage);
        return 2;
    }

    using var judgeModel = settings.CreateChatClient(settings.JudgeModel);
    var judge = new LlmReplyJudge(judgeModel);
    var drafts = Evaluator.Load(Evaluator.DatasetPath(language));
    var reports = new List<RunReport>();

    for (var run = 1; run <= runs; run++)
    {
        var report = await Evaluator.RunOnceAsync(run, drafts, judge, TimeSpan.FromSeconds(60), language);
        reports.Add(report);
        Console.WriteLine($"Run {run}: correct {report.Correct}/{report.Total} · false negatives {report.FalseNegatives} · " +
                          $"false positives {report.FalsePositives} · judge misses {report.JudgeMisses}/{report.JudgeReached}");
        foreach (var miss in report.Items.Where(i => i.Actual != i.Expected))
            Console.WriteLine($"   {miss.Id} expected {miss.Expected}, got {miss.Actual} at {miss.Stage}: {miss.Note}");
    }

    var evaluation = new EvaluationReport(settings.JudgeModel, language.ToString().ToLowerInvariant(), DateTimeOffset.UtcNow, reports);
    var dir = Path.Combine(Directory.GetCurrentDirectory(), "results");
    Directory.CreateDirectory(dir);
    var file = Path.Combine(dir, $"{evaluation.At:yyyyMMdd-HHmmss}-{evaluation.Language}-{string.Join("_", settings.JudgeModel.Split(Path.GetInvalidFileNameChars()))}.json");
    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(evaluation, Evaluator.Json));
    Console.WriteLine($"Results written to {file}");
    return 0;
}
