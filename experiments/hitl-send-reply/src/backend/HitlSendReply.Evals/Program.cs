using HitlSendReply.Evals;
using HitlSendReply.Tickets;

if (args.FirstOrDefault()?.ToLowerInvariant() != "evaluate")
{
    Console.WriteLine("""
        HITL send_reply: judge measurement

          dotnet run --project src/backend/HitlSendReply.Evals -- evaluate [--lang en|es] [--runs 3] [--set dev|blind]
              Runs the full policy with the real judge over labeled drafts and writes results/.
              Needs OPENAI_API_KEY (or LAB_LLM_* variables). dev = data/drafts.<lang>.jsonl,
              blind = heldout/blind.<lang>.jsonl (never used to tune).
        """);
    return 0;
}

var language = (Option("--lang") ?? "en").Equals("es", StringComparison.OrdinalIgnoreCase) ? Language.Es : Language.En;
var set = (Option("--set") ?? "dev").ToLowerInvariant() == "blind" ? "blind" : "dev";
return await EvaluateCommand.RunAsync(int.Parse(Option("--runs") ?? "3"), language, set);

string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
