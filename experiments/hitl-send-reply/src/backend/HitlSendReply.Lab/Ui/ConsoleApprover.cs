using Spectre.Console;
using HitlSendReply.SendReply;

namespace HitlSendReply.Lab.Ui;

/// <summary>Terminal reviewer: approve once, or reject with a reason. There is no "always approve".</summary>
public sealed class ConsoleApprover(TextReader input, string reviewer) : IHumanApprover
{
    public async Task<HumanVerdict> ReviewAsync(ApprovalPrompt prompt, CancellationToken cancellationToken)
    {
        var t = ConsoleUi.T;
        var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();
        grid.AddRow($"[grey]{t.Why}[/]", $"[gold1]{t.W(prompt.Decision.Stage.ToString())}[/] [grey]·[/] [gold1]{Markup.Escape(prompt.Decision.Reason)}[/]");
        grid.AddRow($"[grey]{t.Ticket}[/]", $"[white]{prompt.TicketId}[/]");
        grid.AddRow($"[grey]{t.Recipient}[/]", $"[white]{Markup.Escape(prompt.Recipient)}[/] [grey50]{t.FromTicket}[/]");
        grid.AddRow($"[grey]{t.Channel}[/]", $"[white]{t.W(prompt.Channel.ToString())}[/]");
        grid.AddRow($"[grey]{t.ExactText}[/]", $"[bold white]“{Markup.Escape(prompt.Text)}”[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(grid)
            .Header($"[bold black on gold1] {t.ReviewRequired} [/]")
            .Border(BoxBorder.Heavy).BorderColor(Color.Gold1).Padding(1, 1).Expand());

        AnsiConsole.Markup($"[bold]{t.ApproveQuestion}[/] [green]y[/] {t.ApproveOnce} [grey]·[/] [red]n[/] {t.RejectWithReason} [grey]›[/] ");
        var answer = await ReadAsync(cancellationToken);
        if (string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[green]{t.Approved}[/] [grey]{t.BoundToText}[/]\n");
            return HumanVerdict.Approve(reviewer);
        }

        AnsiConsole.Markup($"[bold]{t.Reason}[/] [grey]{t.SentBack} ›[/] ");
        var reason = await ReadAsync(cancellationToken);
        AnsiConsole.MarkupLine($"[red]{t.Rejected}[/]\n");
        return HumanVerdict.Reject(reviewer, string.IsNullOrWhiteSpace(reason) ? t.NoReason : reason);
    }

    private async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var line = (await input.ReadLineAsync(cancellationToken))?.Trim() ?? "";
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine($"[bold deepskyblue1]{Markup.Escape(line)}[/]"); // echo piped answers
        }

        return line;
    }
}
