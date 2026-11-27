using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using HitlSendReply.Application.Runs;
using HitlSendReply.Contracts;

namespace HitlSendReply.Presentation.Endpoints;

// Inside the namespace so it wins over HitlSendReply.Presentation.Results.
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>Group <c>api/inbound-emails</c>: the webhook an email provider calls when support receives an email.</summary>
public sealed class InboundEmailEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder inbound = app.MapGroup("api/inbound-emails").WithTags("Inbound");

        // 202 when a run was registered (it starts in the background), 200 for Unmatched and Duplicate.
        inbound.MapPost(string.Empty, async (InboundEmailRequest email, ICommandHandler<ReceiveInboundEmailCommand, InboundEmailResult> handler, CancellationToken ct) =>
                (await handler.Handle(new ReceiveInboundEmailCommand(email.EmailId, email.From, email.Subject, email.Body), ct))
                .Match(result => result.Status == InboundEmailStatus.Started
                    ? Results.Accepted($"/api/runs/{result.RunId}", result)
                    : Results.Ok(result), ApiResults.Problem))
            .WithName("ReceiveInboundEmail")
            .Produces<InboundEmailResult>(StatusCodes.Status202Accepted)
            .Produces<InboundEmailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
