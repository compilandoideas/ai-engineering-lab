using Spectre.Console;
using HitlSendReply.Api.Live;
using HitlSendReply.Api.Runs;
using HitlSendReply.Lab.Ui;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Live;

/// <summary><c>live</c>: real agent model and real judge on one ticket. The person at the terminal is the reviewer.</summary>
public static class LiveCommand
{
    public static async Task<int> RunAsync(string ticketId, string message, Language language)
    {
        if (LlmSettings.FromEnvironment() is not { } settings)
        {
            Console.Error.WriteLine(LlmSettings.MissingMessage);
            return 2;
        }

        using var agentModel = settings.CreateChatClient(settings.AgentModel);
        using var judgeModel = settings.CreateChatClient(settings.JudgeModel);
        var auditPath = Path.Combine(Directory.GetCurrentDirectory(), "audit.jsonl");
        var pipeline = Pipeline.Create(agentModel, settings.CreateJudge(judgeModel), new ConsoleApprover(Console.In, Environment.UserName), language, auditPath: auditPath);

        ConsoleUi.Banner(ConsoleUi.T.LiveModel);
        ConsoleUi.LiveHeader($"agent {settings.AgentModel} · judge {settings.JudgeModel} ({settings.JudgeKind})", ticketId, message);
        var result = await pipeline.Runner.RunAsync(ticketId, message);
        ConsoleUi.Trace(pipeline.Audit);
        ConsoleUi.Result(pipeline.Outbox, 0);
        AnsiConsole.MarkupLine($"[grey]Agent:[/] {Markup.Escape(result.FinalText)}\n[grey]Audit appended to {Markup.Escape(auditPath)}[/]");
        return 0;
    }
}
