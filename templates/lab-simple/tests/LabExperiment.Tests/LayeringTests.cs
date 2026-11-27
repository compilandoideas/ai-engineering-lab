using LabExperiment.Core.Results;
using NetArchTest.Rules;

namespace LabExperiment.Tests;

/// <summary>
/// The dependency rule on the compiled assembly: Core knows neither the host nor a provider SDK
/// or agent framework; it reaches a model only through IChatClient.
/// </summary>
public class LayeringTests
{
    [Fact]
    public void Core_knows_no_host_no_provider_and_no_agent_framework()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .ShouldNot().HaveDependencyOnAny("LabExperiment.Console", "OpenAI", "Azure.AI", "Microsoft.Agents.AI")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Core depends on: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
