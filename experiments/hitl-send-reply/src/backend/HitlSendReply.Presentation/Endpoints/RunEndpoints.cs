using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using HitlSendReply.Application.Runs;
using HitlSendReply.Contracts;

namespace HitlSendReply.Presentation.Endpoints;

// Inside the namespace so it wins over HitlSendReply.Presentation.Results.
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>Group <c>api/runs</c>: read a run, and give a person's verdict on one of its pending reviews.</summary>
public sealed class RunEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder runs = app.MapGroup("api/runs").WithTags("Runs");

        runs.MapGet("{runId}", async (string runId, IQueryHandler<GetRunQuery, RunDto> handler, CancellationToken ct) =>
                (await handler.Handle(new GetRunQuery(runId), ct)).Match(Results.Ok, ApiResults.Problem))
            .WithName("GetRun");

        runs.MapPost("{runId}/reviews/{callId}", async (string runId, string callId, ReviewRequest review,
                    ICommandHandler<ReviewRunCommand, RunDto> handler, CancellationToken ct) =>
                (await handler.Handle(new ReviewRunCommand(runId, callId, review.Approved, review.Reason, review.EditedText, review.Reviewer), ct))
                .Match(Results.Ok, ApiResults.Problem))
            .WithName("ReviewRun");
    }
}
