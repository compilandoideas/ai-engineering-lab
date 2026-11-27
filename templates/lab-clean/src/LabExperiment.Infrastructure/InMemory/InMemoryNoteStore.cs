using System.Collections.Concurrent;
using LabExperiment.Application.Abstractions;
using LabExperiment.Domain.Notes;

namespace LabExperiment.Infrastructure.InMemory;

internal sealed class InMemoryNoteStore : INoteStore
{
    private readonly ConcurrentDictionary<Guid, Note> _notes = new();

    public Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        _notes[note.Id] = note;
        return Task.CompletedTask;
    }

    public Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_notes.GetValueOrDefault(id));
}
