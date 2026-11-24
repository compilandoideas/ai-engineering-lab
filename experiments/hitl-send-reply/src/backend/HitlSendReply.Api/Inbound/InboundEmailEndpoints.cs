using HitlSendReply.Contracts;

namespace HitlSendReply.Api.Inbound;

public static class InboundEmailEndpoints
{
    public static void MapInboundEmailEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/inbound-emails", Handle).WithTags("Inbound").WithName("ReceiveInboundEmail")
            .Produces<InboundEmailResult>(StatusCodes.Status202Accepted)
            .Produces<InboundEmailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    // 202 when a run was registered (it starts in the background), 200 for Unmatched and Duplicate.
    private static IResult Handle(InboundEmailRequest email, InboundEmailHandler handler)
    {
        if (string.IsNullOrWhiteSpace(email.EmailId) || string.IsNullOrWhiteSpace(email.From))
        {
            return TypedResults.Problem("emailId and from are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = handler.Handle(email);
        return result.Status == InboundEmailStatus.Started
            ? TypedResults.Accepted($"/api/runs/{result.RunId}", result)
            : TypedResults.Ok(result);
    }
}
