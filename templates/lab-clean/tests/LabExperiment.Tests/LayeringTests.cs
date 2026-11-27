using System.Reflection;
using LabExperiment.Api;
using LabExperiment.Application;
using LabExperiment.Domain;
using LabExperiment.Infrastructure;
using LabExperiment.Presentation.Endpoints;
using NetArchTest.Rules;

namespace LabExperiment.Tests;

/// <summary>
/// The dependency rule, checked on the compiled assemblies. Domain knows nothing. Application knows
/// Domain, never an adapter, the web or a model framework. Presentation knows Application, never an
/// adapter. Infrastructure holds the agent framework and the model clients. Api only composes.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Result).Assembly;
    private static readonly Assembly Application = typeof(ApplicationConfiguration).Assembly;
    private static readonly Assembly Infrastructure = typeof(InfrastructureConfiguration).Assembly;
    private static readonly Assembly Presentation = typeof(IEndpoint).Assembly;
    private static readonly Assembly Api = typeof(ApiMarker).Assembly;

    private static readonly string[] ModelFrameworks = ["Microsoft.Agents.AI", "Microsoft.Extensions.AI", "OpenAI"];

    private static List<string> References(Assembly assembly) => assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    private static string Name(Assembly assembly) => assembly.GetName().Name!;

    private static bool IsModelFramework(string reference) => ModelFrameworks.Any(f => reference.StartsWith(f, StringComparison.Ordinal));

    [Fact]
    public void Domain_references_nothing_in_the_solution_nor_a_framework()
    {
        var references = References(Domain);

        Assert.DoesNotContain(references, r => r.StartsWith("LabExperiment", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => IsModelFramework(r)
            || r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || r.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_knows_no_adapter_no_web_and_no_model_framework()
    {
        var references = References(Application);

        Assert.DoesNotContain(Name(Infrastructure), references);
        Assert.DoesNotContain(Name(Presentation), references);
        Assert.DoesNotContain(Name(Api), references);
        Assert.DoesNotContain(references, r => IsModelFramework(r) || r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Presentation_sends_commands_and_knows_no_adapter()
    {
        var references = References(Presentation);

        Assert.Contains(Name(Application), references);
        Assert.DoesNotContain(Name(Infrastructure), references);
        Assert.DoesNotContain(references, IsModelFramework);
    }

    [Fact]
    public void Infrastructure_knows_no_endpoint_and_no_host()
    {
        var references = References(Infrastructure);

        Assert.DoesNotContain(Name(Presentation), references);
        Assert.DoesNotContain(Name(Api), references);
        Assert.DoesNotContain(references, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void No_type_in_Domain_or_Application_depends_on_a_model_framework_or_an_adapter()
    {
        foreach (var assembly in new[] { Domain, Application })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny([.. ModelFrameworks, Name(Infrastructure)])
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{Name(assembly)} depends on a framework or an adapter: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void The_rule_detects_a_violation()
    {
        // Guard against a rule that passes vacuously: the model adapter *does* depend on Microsoft.Extensions.AI.
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot().HaveDependencyOnAny(ModelFrameworks)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }
}
