using System.Net;
using System.Net.Http.Json;
using HitlSendReply.Contracts;
using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Mailbox;

public interface IRunStatusReader
{
    /// <summary>What the run did so far, or null when the API does not know the run (it keeps runs in memory).</summary>
    Task<RunState?> ReadAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>
/// The run as the inbox shows it: a status for the row, if starting failed why, the reviews still
/// waiting for a person and the verdicts people gave.
/// </summary>
public sealed record RunState(
    EmailStatus Status, string? Error = null,
    IReadOnlyList<PendingReply>? Pending = null, IReadOnlyList<ReplyVerdict>? Verdicts = null);

public sealed record PendingReply(string CallId, string Recipient, string Text);

/// <summary>A person's verdict. FinalText is what reached the customer, when something did; Edited when the person wrote it.</summary>
public sealed record ReplyVerdict(
    string CallId, bool Approved, string? Reason, string? Reviewer, DateTimeOffset At, bool Edited = false, string? FinalText = null);

/// <summary>Reads a run back from the Agent API (GET /api/runs/{id}) to show it on the email's row.</summary>
public sealed class RunStatusReader(HttpClient http) : IRunStatusReader
{
    public async Task<RunState?> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/runs/{runId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<RunDto>(cancellationToken);
        if (run is null)
        {
            return null;
        }

        var (status, error) = run switch
        {
            { Status: RunStatus.Starting } => (EmailStatus.Notified, (string?)null),
            { Status: RunStatus.Failed } => (EmailStatus.RunFailed, run.Error),
            { Status: RunStatus.AwaitingReview } => (EmailStatus.AwaitingReview, null),
            { Sent.Count: > 0 } => (EmailStatus.Sent, null),
            _ => (EmailStatus.Blocked, null),
        };
        var pending = run.PendingReviews.Select(p => new PendingReply(p.CallId, p.Recipient, p.Text)).ToList();
        var verdicts = run.Audit
            .Where(a => a.CallId is not null && a.Event is "human-approved" or "human-rejected" or "human-edited")
            .Select(a => new ReplyVerdict(a.CallId!, a.Event != "human-rejected", a.Reason, a.Actor, a.At,
                Edited: a.Event == "human-edited", FinalText: run.Sent.FirstOrDefault(s => s.TextHash == a.TextHash)?.Text))
            .ToList();
        return new RunState(status, error, pending, verdicts);
    }
}
