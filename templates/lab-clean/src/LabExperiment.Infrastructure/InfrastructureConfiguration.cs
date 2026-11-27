using LabExperiment.Application.Abstractions;
using LabExperiment.Infrastructure.InMemory;
using LabExperiment.Infrastructure.Scripted;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LabExperiment.Infrastructure;

public static class InfrastructureConfiguration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<INoteStore, InMemoryNoteStore>();

        // The model. Scripted by default so the experiment runs with no key; replace it with a real
        // IChatClient (and the MAF agent built on it) here, never in Application.
        services.TryAddSingleton<IChatClient>(new ScriptedChatClient(["Scripted response."]));

        return services;
    }
}
