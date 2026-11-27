using LabExperiment.Domain.Notes;

namespace LabExperiment.Application.Abstractions;

// A port: Application declares it, Infrastructure implements it.
public interface INoteStore
{
    Task AddAsync(Note note, CancellationToken cancellationToken);

    Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken);
}
