using System.Net.Http.Json;
using HitlSendReply.Contracts;

namespace HitlSendReply.Web.Client.Api;

/// <summary>
/// The only way the UI reaches the agent: the HTTP contract under /api. An error comes back as the
/// problem detail the API wrote, so the screen can show it as is.
/// </summary>
public sealed class AgentApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ScenarioDto>> ListScenariosAsync(string lang, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<ScenarioDto>>($"api/scenarios?lang={lang}", cancellationToken) ?? [];

    public async Task<IReadOnlyList<TicketDto>> ListTicketsAsync(string lang, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<TicketDto>>($"api/tickets?lang={lang}", cancellationToken) ?? [];

    public async Task<ApiResult<RunDto>> GetRunAsync(string runId, CancellationToken cancellationToken = default) =>
        await ReadAsync<RunDto>(await http.GetAsync($"api/runs/{runId}", cancellationToken), cancellationToken);

    public async Task<ApiResult<RunDto>> StartRunAsync(StartRunRequest request, CancellationToken cancellationToken = default) =>
        await ReadAsync<RunDto>(await http.PostAsJsonAsync("api/runs", request, cancellationToken), cancellationToken);

    public async Task<ApiResult<RunDto>> ReviewAsync(string runId, string callId, ReviewRequest review, CancellationToken cancellationToken = default) =>
        await ReadAsync<RunDto>(await http.PostAsJsonAsync($"api/runs/{runId}/reviews/{callId}", review, cancellationToken), cancellationToken);

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(cancellationToken), null);
        }

        var problem = await TryReadProblemAsync(response, cancellationToken);
        return new ApiResult<T>(default, problem?.Detail ?? problem?.Title ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }

    private static async Task<Problem?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<Problem>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record Problem(string? Title, string? Detail);
}

/// <summary>A call's value, or why it failed and the HTTP status (422: an edited text broke a rule).</summary>
public sealed record ApiResult<T>(T? Value, string? Error, int? Status = null);
