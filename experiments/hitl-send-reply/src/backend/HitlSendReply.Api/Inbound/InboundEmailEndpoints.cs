using HitlSendReply.Contracts;

namespace HitlSendReply.Api.Inbound;

public static class InboundEmailEndpoints
{
    public static void MapInboundEmailEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/inbound-emails", HandleAsync).WithTags("Inbound").WithName("ReceiveInboundEmail");

    private static async Task<IResult> HandleAsync(InboundEmailRequest email, InboundEmailHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.EmailId) || string.IsNullOrWhiteSpace(email.From))
        {
            return TypedResults.Problem("emailId and from are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.Ok(await handler.HandleAsync(email, cancellationToken));
    }
}
