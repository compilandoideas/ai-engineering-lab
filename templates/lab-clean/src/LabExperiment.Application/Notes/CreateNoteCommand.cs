using FluentValidation;
using LabExperiment.Application.Abstractions;
using LabExperiment.Application.Messaging;
using LabExperiment.Domain;
using LabExperiment.Domain.Notes;

namespace LabExperiment.Application.Notes;

public sealed record CreateNoteCommand(string Text) : ICommand<Guid>;

internal sealed class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(c => c.Text).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateNoteCommandHandler(INoteStore store, TimeProvider clock)
    : ICommandHandler<CreateNoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateNoteCommand command, CancellationToken cancellationToken)
    {
        var note = new Note(Guid.NewGuid(), command.Text.Trim(), clock.GetUtcNow());
        await store.AddAsync(note, cancellationToken);
        return note.Id;
    }
}
