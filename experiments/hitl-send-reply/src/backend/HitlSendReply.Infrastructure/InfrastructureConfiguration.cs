using Microsoft.Extensions.DependencyInjection;

namespace HitlSendReply.Infrastructure;

public static class InfrastructureConfiguration
{
    /// <summary>The adapters behind the application's ports: models from the environment, sample tickets, MAF runs.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(_ => new LiveModels(LlmSettings.FromEnvironment()));
        services.AddSingleton<ITicketDirectory, SampleTicketDirectory>();
        services.AddSingleton<IRunSessionFactory, RunSessionFactory>();
        return services;
    }
}
