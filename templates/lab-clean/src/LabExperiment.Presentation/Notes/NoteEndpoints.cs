using LabExperiment.Application.Messaging;
using LabExperiment.Application.Notes;
using LabExperiment.Presentation.Endpoints;
using LabExperiment.Presentation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LabExperiment.Presentation.Notes;

internal sealed class NoteEndpoints : IEndpoint
{
    public sealed record CreateNoteRequest(string Text);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/notes").WithTags("Notes");

        notes.MapPost("/", async (CreateNoteRequest request, ICommandHandler<CreateNoteCommand, Guid> handler, CancellationToken ct) =>
            (await handler.Handle(new CreateNoteCommand(request.Text), ct))
                .Match(id => Microsoft.AspNetCore.Http.Results.Created($"/api/notes/{id}", new { id }), ApiResults.Problem));

        notes.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetNoteQuery, NoteResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetNoteQuery(id), ct))
                .Match(Microsoft.AspNetCore.Http.Results.Ok, ApiResults.Problem));
    }
}
