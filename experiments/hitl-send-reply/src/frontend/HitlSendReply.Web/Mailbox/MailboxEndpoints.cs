using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// The screen's mailbox API, on the Web server (not forwarded to the Agent API): the inbox is
/// this app's data, the agent only receives the webhook.
/// </summary>
public static class MailboxEndpoints
{
    public static void MapMailboxEndpoints(this IEndpointRouteBuilder app)
    {
        var emails = app.MapGroup("/mailbox/emails");
        emails.MapPost("/", SubmitAsync);
        emails.MapGet("/", ListAsync);
        emails.MapGet("/{id:guid}", GetAsync);
        emails.MapPost("/{id:guid}/retry", RetryAsync);
    }

    private static async Task<IResult> SubmitAsync(ComposeEmailRequest request, MailboxService mailbox, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            return TypedResults.Problem("from, subject and body are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var row = await mailbox.SubmitAsync(request, cancellationToken);
        return TypedResults.Created($"/mailbox/emails/{row.Id}", row);
    }

    private static async Task<EmailPageDto> ListAsync(MailboxService mailbox, CancellationToken cancellationToken,
        string? search = null, string? sort = null, bool desc = false, int page = 0, int pageSize = 10) =>
        await mailbox.ListAsync(new EmailQuery(search, sort, desc, Math.Max(page, 0), Math.Clamp(pageSize, 1, 100)), cancellationToken);

    private static async Task<IResult> GetAsync(Guid id, MailboxService mailbox, CancellationToken cancellationToken) =>
        await mailbox.GetAsync(id, cancellationToken) is { } row ? TypedResults.Ok(row) : TypedResults.NotFound();

    private static async Task<IResult> RetryAsync(Guid id, MailboxService mailbox, CancellationToken cancellationToken) =>
        await mailbox.RetryAsync(id, cancellationToken) is { } row ? TypedResults.Ok(row) : TypedResults.NotFound();
}
