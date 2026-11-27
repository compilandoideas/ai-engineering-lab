using LabExperiment.Application.Abstractions;
using LabExperiment.Application.Messaging;
using LabExperiment.Domain;

namespace LabExperiment.Application.Notes;

public sealed record NoteResponse(Guid Id, string Text, DateTimeOffset CreatedAt);

public sealed record GetNoteQuery(Guid Id) : IQuery<NoteResponse>;

public static class NoteErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("Notes.NotFound", $"The note {id} was not found.");
}

internal sealed class GetNoteQueryHandler(INoteStore store) : IQueryHandler<GetNoteQuery, NoteResponse>
{
    public async Task<Result<NoteResponse>> Handle(GetNoteQuery query, CancellationToken cancellationToken)
    {
        var note = await store.FindAsync(query.Id, cancellationToken);
        return note is null
            ? Result.Failure<NoteResponse>(NoteErrors.NotFound(query.Id))
            : new NoteResponse(note.Id, note.Text, note.CreatedAt);
    }
}
