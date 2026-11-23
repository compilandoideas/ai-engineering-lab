using HitlSendReply.Lab.Evaluate;
using HitlSendReply.Lab.Live;
using HitlSendReply.Lab.Scenarios;
using HitlSendReply.Lab.Ui;
using HitlSendReply.Tickets;

ConsoleUi.Configure();
ConsoleUi.T = UiText.For(Lang());
var command = args.FirstOrDefault()?.ToLowerInvariant();
return command switch
{
    "scenario" => await ScenarioCommand.RunAsync(args.ElementAtOrDefault(1) is { } w && !w.StartsWith("--", StringComparison.Ordinal) ? w : "all", Lang()),
    "live" => await LiveCommand.RunAsync(Option("--ticket") ?? "T-1002", Option("--message") ?? DefaultMessage(Lang()), Lang()),
    "evaluate" => await EvaluateCommand.RunAsync(int.Parse(Option("--runs") ?? "3"), Lang()),
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
