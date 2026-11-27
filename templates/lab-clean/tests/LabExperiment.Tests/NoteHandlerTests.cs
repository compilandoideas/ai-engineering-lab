using LabExperiment.Application;
using LabExperiment.Application.Messaging;
using LabExperiment.Application.Notes;
using LabExperiment.Domain;
using LabExperiment.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LabExperiment.Tests;

// Sample tests: delete them with the Notes feature. They resolve the handlers from DI, so the
// decorators (validation, logging, exceptions) run as in the API.
public class NoteHandlerTests
{
    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddApplication([typeof(ApplicationConfiguration).Assembly])
            .AddInfrastructure()
            .BuildServiceProvider();

    [Fact]
    public async Task A_created_note_can_be_read_back()
    {
        using var services = BuildServices();
        var create = services.GetRequiredService<ICommandHandler<CreateNoteCommand, Guid>>();
        var get = services.GetRequiredService<IQueryHandler<GetNoteQuery, NoteResponse>>();

        var created = await create.Handle(new CreateNoteCommand("  first note "), CancellationToken.None);
        var read = await get.Handle(new GetNoteQuery(created.Value), CancellationToken.None);

        Assert.True(read.IsSuccess);
        Assert.Equal("first note", read.Value.Text);
    }

    [Fact]
    public async Task The_validation_decorator_refuses_an_empty_note()
    {
        using var services = BuildServices();
        var create = services.GetRequiredService<ICommandHandler<CreateNoteCommand, Guid>>();

        var result = await create.Handle(new CreateNoteCommand(""), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task A_missing_note_is_not_found()
    {
        using var services = BuildServices();
        var get = services.GetRequiredService<IQueryHandler<GetNoteQuery, NoteResponse>>();

        var result = await get.Handle(new GetNoteQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
