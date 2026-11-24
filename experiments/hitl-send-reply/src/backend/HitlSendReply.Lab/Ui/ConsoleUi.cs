using Spectre.Console;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Policy;
using HitlSendReply.SendReply;

namespace HitlSendReply.Lab.Ui;

/// <summary>
/// Presentation only. Nothing here decides anything: it renders what the pipeline did and reads
/// the reviewer's answer. Tests never touch this class.
/// </summary>
public static class ConsoleUi
{
    public static UiText T { get; set; } = UiText.En;

    /// <summary>Set LAB_FORCE_COLOR=1 to keep colors when output is redirected (used for screenshots).</summary>
    public static void Configure()
    {
        if (Environment.GetEnvironmentVariable("LAB_FORCE_COLOR") != "1")
        {
            if (Console.IsOutputRedirected)
            {
                AnsiConsole.Profile.Width = 112;
            }

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
        AnsiConsole.Write(new Rule($"[bold mediumpurple1]{T.Scenario} {scenario.Number}[/] [white]{Markup.Escape(scenario.Name)}[/]").LeftJustified().RuleStyle("mediumpurple4"));

        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow($"[grey]{T.Ticket}[/]", $"[white]{scenario.Ticket.Id}[/] [grey]· {T.Order} #{scenario.Ticket.Order.Id} · {T.W(scenario.Ticket.Order.Status.ToString())} · {T.W(scenario.Ticket.Channel.ToString())}[/]");
        grid.AddRow($"[grey]{T.Customer}[/]", $"[italic]“{Markup.Escape(scenario.CustomerMessage)}”[/]");
        grid.AddRow($"[grey]{T.AgentDraft}[/]", $"[white]“{Markup.Escape(scenario.Drafts[0])}”[/]");
        grid.AddRow($"[grey]{T.Expected}[/]", $"[grey]{Markup.Escape(scenario.Expectation)}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey50]{Markup.Escape(T.Offline)}[/]");
    }

    public static void LiveHeader(string model, string ticketId, string message)
    {
        AnsiConsole.WriteLine();
        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow($"[grey]{T.Model}[/]", $"[white]{Markup.Escape(model)}[/]");
        grid.AddRow($"[grey]{T.Ticket}[/]", $"[white]{Markup.Escape(ticketId)}[/]");
        grid.AddRow($"[grey]{T.Customer}[/]", $"[italic]“{Markup.Escape(message)}”[/]");
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    public static void Trace(IAuditLog audit)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey23).Expand()
            .Title($"[grey]{T.AuditTitle}[/]")
            .AddColumn($"[grey]{T.Step}[/]")
            .AddColumn($"[grey]{T.Decision}[/]")
            .AddColumn($"[grey]{T.Stage}[/]")
            .AddColumn($"[grey]{T.By}[/]")
            .AddColumn($"[grey]{T.Reason.ToLowerInvariant()}[/]")
            .AddColumn($"[grey]{T.TextHash}[/]");

        foreach (var e in audit.Entries)
        {
            table.AddRow(
                $"[white]{Markup.Escape(T.W(e.Event))}[/]",
                Badge(e.Event, e.Outcome),
                $"[grey]{Markup.Escape(T.W(e.Stage))}[/]",
                $"[deepskyblue1]{Markup.Escape(T.W(e.Actor))}[/]",
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
            AnsiConsole.Write(new Panel($"[bold red]{T.NotSent}[/]  [grey]{T.NothingReached}[/]")
                .Border(BoxBorder.Rounded).BorderColor(Color.Red3).Padding(1, 0));
            return;
        }

        foreach (var s in sent)
        {
            AnsiConsole.Write(new Panel(
                    $"[bold green]{T.Sent}[/] [grey]{T.To}[/] [white]{Markup.Escape(s.Recipient)}[/] [grey]{T.Via}[/] [white]{T.W(s.Channel.ToString())}[/]\n[white]“{Markup.Escape(s.Text)}”[/]")
                .Border(BoxBorder.Rounded).BorderColor(Color.Green3).Padding(1, 0));
        }
    }

    private static string Badge(string @event, string? outcome) => (@event, outcome) switch
    {
        ("sent", _) => $"[black on green3] {T.Sent} [/]",
        ("send-refused" or "send-duplicate", _) => $"[white on red3] {T.NotSent} [/]",
        (_, "AutoSend") => $"[black on green3] {T.AutoSend} [/]",
        (_, "NeedsHuman") => $"[black on gold1] {T.Human} [/]",
        (_, "Block") => $"[white on red3] {T.Block} [/]",
        (_, "Approved") => $"[black on green3] {T.ApprovedBadge} [/]",
        (_, "Rejected") => $"[white on red3] {T.RejectedBadge} [/]",
        _ => Markup.Escape(outcome ?? ""),
    };
}
