using System.Net.Http.Json;

namespace HitlSendReply.Web.Client.Mailbox;

/// <summary>The screen's calls to the Web server's mailbox (/mailbox/emails), not to the Agent API.</summary>
public sealed class MailboxClient(HttpClient http)
{
    public async Task<EmailPageDto> ListAsync(string? search, string? sort, bool descending, int page, int pageSize,
        MailFolder folder = MailFolder.All, CancellationToken cancellationToken = default)
    {
        var query = $"mailbox/emails?page={page}&pageSize={pageSize}&desc={descending.ToString().ToLowerInvariant()}"
            + (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search)}")
            + (string.IsNullOrWhiteSpace(sort) ? "" : $"&sort={Uri.EscapeDataString(sort)}")
            + (folder == MailFolder.All ? "" : $"&folder={folder}");
        return await http.GetFromJsonAsync<EmailPageDto>(query, cancellationToken) ?? new EmailPageDto([], 0);
    }

    public async Task<EmailRowDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"mailbox/emails/{id}", cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EmailRowDto>(cancellationToken) : null;
    }

    public async Task<EmailRowDto?> SubmitAsync(ComposeEmailRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("mailbox/emails", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmailRowDto>(cancellationToken);
    }

    public async Task<EmailRowDto?> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"mailbox/emails/{id}/retry", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmailRowDto>(cancellationToken);
    }
}
