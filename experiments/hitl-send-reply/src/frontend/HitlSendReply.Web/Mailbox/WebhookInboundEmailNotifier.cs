using System.Net.Http.Json;
using HitlSendReply.Contracts;

namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// POST /api/inbound-emails, like an email provider's inbound-parse webhook. Retries from the
/// standard resilience handler are safe: the API is idempotent by EmailId.
/// </summary>
public sealed class WebhookInboundEmailNotifier(HttpClient http) : IInboundEmailNotifier
{
    public async Task<InboundEmailResult> NotifyAsync(InboundEmail email, CancellationToken cancellationToken)
    {
        var request = new InboundEmailRequest(email.Id.ToString("n"), email.From, email.Subject, email.Body, email.ReceivedAt);
        using var response = await http.PostAsJsonAsync("api/inbound-emails", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InboundEmailResult>(cancellationToken)
            ?? throw new InvalidOperationException("The inbound webhook returned no result.");
    }
}
