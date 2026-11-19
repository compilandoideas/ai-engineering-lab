using HitlSendReply.Agent;
using HitlSendReply.Publishing;
using Spectre.Console;
using HitlSendReply.Policy;

namespace HitlSendReply.Cli;

/// <summary>
/// Presentation only. Nothing here decides anything: it renders what the pipeline did and reads
/// the reviewer's answer. Tests never touch this class.
/// </summary>
public static class ConsoleUi
{
    /// <summary>Set LAB_FORCE_COLOR=1 to keep colors when output is redirected (used for screenshots).</summary>
    public static void Configure()
    {
        if (Environment.GetEnvironmentVariable("LAB_FORCE_COLOR") != "1")
        {
            if (Console.IsOutputRedirected)
                AnsiConsole.Profile.Width = 112;
            return;
        }

        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        AnsiConsole.Profile.Width = 112;
    }

    public static void Banner(string subtitle)
    {
        AnsiConsole.Write(new Rule($"[bold deepskyblue1]HITL · send_reply[/] [grey]· {Markup.Escape(subtitle)}[/]").LeftJustified().RuleStyle("grey23"));
    }

    public static void ScenarioHeader(Scenario scenario)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold mediumpurple1]Scenario {scenario.Number}[/] [white]{Markup.Escape(scenario.Name)}[/]").LeftJustified().RuleStyle("mediumpurple4"));

        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow("[grey]Ticket[/]", $"[white]{scenario.Ticket.Id}[/] [grey]· order #{scenario.Ticket.Order.Id} · {scenario.Ticket.Order.Status} · {scenario.Ticket.Channel}[/]");
        grid.AddRow("[grey]Customer[/]", $"[italic]“{Markup.Escape(scenario.CustomerMessage)}”[/]");
        grid.AddRow("[grey]Agent draft[/]", $"[white]“{Markup.Escape(scenario.Drafts[0])}”[/]");
        grid.AddRow("[grey]Expected[/]", $"[grey]{Markup.Escape(scenario.Expectation)}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey50]offline mode: the agent's draft and the judge's signal are scripted[/]");
    }

    public static void LiveHeader(string model, string ticketId, string message)
    {
        AnsiConsole.WriteLine();
        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow("[grey]Model[/]", $"[white]{Markup.Escape(model)}[/]");
        grid.AddRow("[grey]Ticket[/]", $"[white]{Markup.Escape(ticketId)}[/]");
        grid.AddRow("[grey]Customer[/]", $"[italic]“{Markup.Escape(message)}”[/]");
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    public static void Trace(IAuditLog audit)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey23).Expand()
            .Title("[grey]pipeline · audit log[/]")
            .AddColumn("[grey]step[/]")
            .AddColumn("[grey]decision[/]")
            .AddColumn("[grey]stage[/]")
            .AddColumn("[grey]by[/]")
            .AddColumn("[grey]reason[/]")
            .AddColumn("[grey]text hash[/]");

        foreach (var e in audit.Entries)
        {
            table.AddRow(
                $"[white]{Markup.Escape(e.Event)}[/]",
                Badge(e.Event, e.Outcome),
                $"[grey]{Markup.Escape(e.Stage ?? "")}[/]",
                $"[deepskyblue1]{Markup.Escape(e.Actor ?? "")}[/]",
                Markup.Escape(e.Reason ?? ""),
                $"[grey50]{(e.TextHash.Length >= 8 ? e.TextHash[..8] : e.TextHash)}[/]");
        }

        AnsiConsole.Write(table);
    }

    public static void Result(IOutbox outbox, int sentBefore)
    {
        var sent = outbox.Sent.Skip(sentBefore).ToList();
        if (sent.Count == 0)
        {
            AnsiConsole.Write(new Panel("[bold red]NOT SENT[/]  [grey]nothing reached the customer in this run[/]")
                .Border(BoxBorder.Rounded).BorderColor(Color.Red3).Padding(1, 0));
            return;
        }

        foreach (var s in sent)
        {
            AnsiConsole.Write(new Panel(
                    $"[bold green]SENT[/] [grey]to[/] [white]{Markup.Escape(s.Recipient)}[/] [grey]via[/] [white]{s.Channel}[/]\n[white]“{Markup.Escape(s.Text)}”[/]")
                .Border(BoxBorder.Rounded).BorderColor(Color.Green3).Padding(1, 0));
        }
    }

    private static string Badge(string @event, string? outcome) => (@event, outcome) switch
    {
        ("sent", _) => "[black on green3] SENT [/]",
        ("send-refused" or "send-duplicate", _) => "[white on red3] NOT SENT [/]",
        (_, "AutoSend") => "[black on green3] AUTO-SEND [/]",
        (_, "NeedsHuman") => "[black on gold1] HUMAN [/]",
        (_, "Block") => "[white on red3] BLOCK [/]",
        (_, "Approved") => "[black on green3] APPROVED [/]",
        (_, "Rejected") => "[white on red3] REJECTED [/]",
        _ => Markup.Escape(outcome ?? ""),
    };
}

/// <summary>Terminal reviewer: approve once, or reject with a reason. There is no "always approve".</summary>
public sealed class ConsoleApprover(TextReader input, string reviewer) : IHumanApprover
{
    public async Task<HumanVerdict> ReviewAsync(ApprovalPrompt prompt, CancellationToken cancellationToken)
    {
        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow("[grey]Why[/]", $"[gold1]{Markup.Escape(prompt.Decision.Reason)}[/]");
        grid.AddRow("[grey]Ticket[/]", $"[white]{prompt.TicketId}[/]");
        grid.AddRow("[grey]Recipient[/]", $"[white]{Markup.Escape(prompt.Recipient)}[/] [grey50](from the ticket, not from the model)[/]");
        grid.AddRow("[grey]Channel[/]", $"[white]{prompt.Channel}[/]");
        grid.AddRow("[grey]Exact text[/]", $"[bold white]“{Markup.Escape(prompt.Text)}”[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(grid)
            .Header("[bold black on gold1] HUMAN REVIEW REQUIRED [/]")
            .Border(BoxBorder.Heavy).BorderColor(Color.Gold1).Padding(1, 1).Expand());

        AnsiConsole.Markup("[bold]Approve this exact reply?[/] [green]y[/] approve once [grey]·[/] [red]n[/] reject with reason [grey]›[/] ");
        var answer = await ReadAsync(cancellationToken);
        if (string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[green]✓ approved once[/] [grey]· approval bound to this exact text[/]\n");
            return HumanVerdict.Approve(reviewer);
        }

        AnsiConsole.Markup("[bold]Reason[/] [grey](sent back to the agent) ›[/] ");
        var reason = await ReadAsync(cancellationToken);
        AnsiConsole.MarkupLine("[red]✗ rejected[/]\n");
        return HumanVerdict.Reject(reviewer, string.IsNullOrWhiteSpace(reason) ? "Rejected without reason." : reason);
    }

    private async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var line = (await input.ReadLineAsync(cancellationToken))?.Trim() ?? "";
        if (Console.IsInputRedirected)
            AnsiConsole.MarkupLine($"[bold deepskyblue1]{Markup.Escape(line)}[/]"); // echo piped answers
        return line;
    }
}
