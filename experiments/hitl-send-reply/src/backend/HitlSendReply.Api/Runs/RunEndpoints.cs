using HitlSendReply.Api.Live;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Contracts;
using HitlSendReply.SendReply;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Runs;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var runs = app.MapGroup("/api/runs").WithTags("Runs");
        runs.MapPost("/", StartAsync).WithName("StartRun");
        runs.MapGet("/{runId}", Get).WithName("GetRun");
        runs.MapPost("/{runId}/reviews/{callId}", ReviewAsync).WithName("ReviewRun");
    }

    private static async Task<IResult> StartAsync(StartRunRequest request, RunStore store, LiveModels live, CancellationToken cancellationToken)
    {
        var language = request.Lang.Equals("es", StringComparison.OrdinalIgnoreCase) ? Language.Es : Language.En;
        Pipeline pipeline;
        string ticketId;
        string message;

        if (request.Mode == RunMode.Scenario)
        {
            var scenario = ScenarioCatalog.For(language).SingleOrDefault(s => s.Number == request.Scenario);
            if (scenario is null)
            {
                return TypedResults.Problem("Unknown scenario. Use 1-7.", statusCode: StatusCodes.Status400BadRequest);
            }

            (pipeline, _) = ScenarioHarness.Build(scenario, approver: null);
            (ticketId, message) = (scenario.Ticket.Id, scenario.CustomerMessage);
        }
        else
        {
            if (!live.IsConfigured)
            {
                return TypedResults.Problem(LlmSettings.MissingMessage, title: "No model configured", statusCode: StatusCodes.Status409Conflict);
            }

            if (string.IsNullOrWhiteSpace(request.CustomerMessage))
            {
                return TypedResults.Problem("customerMessage is required in live mode.", statusCode: StatusCodes.Status400BadRequest);
            }

            pipeline = Pipeline.Create(live.Agent, live.CreateJudge(), human: null, language);
            (ticketId, message) = (request.TicketId ?? "T-1002", request.CustomerMessage);
        }

        var run = await pipeline.Runner.StartAsync(ticketId, message, cancellationToken);
        var entry = new RunEntry(Guid.NewGuid().ToString("n"), request.Mode, request.Scenario, ticketId, message, pipeline, run);
        store.Add(entry);
        return TypedResults.Created($"/api/runs/{entry.Id}", RunMapper.ToDto(entry));
    }

    private static IResult Get(string runId, RunStore store) =>
        store.Find(runId) is { } entry ? TypedResults.Ok(RunMapper.ToDto(entry)) : TypedResults.NotFound();

    private static async Task<IResult> ReviewAsync(string runId, string callId, ReviewRequest review, RunStore store, CancellationToken cancellationToken)
    {
        if (store.Find(runId) is not { } entry)
        {
            return TypedResults.NotFound();
        }

        if (!review.Approved && string.IsNullOrWhiteSpace(review.Reason))
        {
            return TypedResults.Problem("A rejection needs a reason: the agent reads it to rewrite.", statusCode: StatusCodes.Status400BadRequest);
        }

        var reviewer = string.IsNullOrWhiteSpace(review.Reviewer) ? "reviewer" : review.Reviewer;
        var verdict = review.Approved ? HumanVerdict.Approve(reviewer) : HumanVerdict.Reject(reviewer, review.Reason!);

        // A run still starting holds the gate while the agent drafts: answer now instead of waiting.
        if (entry.Run is null)
        {
            return TypedResults.Problem(entry.Error ?? "The run is still starting: nothing to review yet.", statusCode: StatusCodes.Status409Conflict);
        }

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (entry.Run is not { } run || run.PendingReviews.All(p => p.CallId != callId))
            {
                return TypedResults.Problem($"No pending review '{callId}' on this run.", statusCode: StatusCodes.Status409Conflict);
            }

            await entry.Pipeline.Runner.ResumeAsync(run, callId, verdict, cancellationToken);
            return TypedResults.Ok(RunMapper.ToDto(entry));
        }
        finally
        {
            entry.Gate.Release();
        }
    }
}
