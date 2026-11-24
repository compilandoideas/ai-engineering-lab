using System.Net;
using System.Net.Http.Json;
using HitlSendReply.Contracts;
using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Mailbox;

public interface IRunStatusReader
{
    /// <summary>What the run did so far, or null when the API does not know the run (it keeps runs in memory).</summary>
    Task<EmailStatus?> ReadAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>Reads a run back from the Agent API (GET /api/runs/{id}) to show it on the email's row.</summary>
public sealed class RunStatusReader(HttpClient http) : IRunStatusReader
{
    public async Task<EmailStatus?> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/runs/{runId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<RunDto>(cancellationToken);
        return run switch
        {
            null => null,
            { Status: RunStatus.AwaitingReview } => EmailStatus.AwaitingReview,
            { Sent.Count: > 0 } => EmailStatus.Sent,
            _ => EmailStatus.Blocked,
        };
    }
}
