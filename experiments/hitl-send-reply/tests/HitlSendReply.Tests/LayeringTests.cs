using System.Reflection;
using NetArchTest.Rules;
using HitlSendReply.Api;
using HitlSendReply.Contracts;
using HitlSendReply.Presentation.Endpoints;
using HitlSendReply.Web.Client.Api;

namespace HitlSendReply.Tests;

/// <summary>
/// The dependency rule, checked on the compiled assemblies. Domain knows nothing. Application knows
/// Domain and the HTTP contract, never an adapter, the web or a model framework. Presentation
/// (minimal API endpoints) knows Application, never an adapter. Infrastructure holds Microsoft Agent
/// Framework and the model clients. Api only composes. The UI reaches the agent through the contract.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Ticket).Assembly;
    private static readonly Assembly Application = typeof(PublishPolicy).Assembly;
    private static readonly Assembly Infrastructure = typeof(SupportRunner).Assembly;
    private static readonly Assembly Presentation = typeof(RunEndpoints).Assembly;
    private static readonly Assembly Api = typeof(ApiMarker).Assembly;
    private static readonly Assembly Contract = typeof(RunDto).Assembly;

    private static readonly string[] ModelFrameworks = ["Microsoft.Agents.AI", "Microsoft.Extensions.AI", "OpenAI"];

    private static List<string> References(Assembly assembly) => assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    private static string Name(Assembly assembly) => assembly.GetName().Name!;

    [Fact]
    public void Domain_references_nothing_in_the_solution_nor_a_framework()
    {
        var references = References(Domain);

        Assert.DoesNotContain(references, r => r.StartsWith("HitlSendReply", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => ModelFrameworks.Any(f => r.StartsWith(f, StringComparison.Ordinal))
            || r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || r.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_knows_no_adapter_no_web_and_no_model_framework()
    {
        var references = References(Application);

        Assert.DoesNotContain(Name(Infrastructure), references);
        Assert.DoesNotContain(Name(Presentation), references);
        Assert.DoesNotContain(Name(Api), references);
        Assert.DoesNotContain(references, r => ModelFrameworks.Any(f => r.StartsWith(f, StringComparison.Ordinal))
            || r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Presentation_sends_commands_and_knows_no_adapter()
    {
        var references = References(Presentation);

        Assert.Contains(Name(Application), references);
        Assert.DoesNotContain(Name(Infrastructure), references);
        Assert.DoesNotContain(references, r => ModelFrameworks.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Infrastructure_knows_no_endpoint_and_no_host()
    {
        var references = References(Infrastructure);

        Assert.DoesNotContain(Name(Presentation), references);
        Assert.DoesNotContain(Name(Api), references);
        Assert.DoesNotContain(references, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HitlSendReply.Domain")]
    [InlineData("HitlSendReply.Application")]
    public void The_decision_does_not_depend_on_the_agent_framework_or_the_llm(string @namespace)
    {
        var assembly = @namespace.EndsWith("Domain", StringComparison.Ordinal) ? Domain : Application;
        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOnAny([.. ModelFrameworks, "HitlSendReply.Infrastructure"])
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{@namespace} depends on a framework or an adapter: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void The_rule_detects_a_violation()
    {
        // Guard against a rule that passes vacuously: the agent adapter *does* depend on MAF.
        var result = Types.InAssembly(Infrastructure)
            .That().ResideInNamespace("HitlSendReply.Infrastructure.Agent")
            .ShouldNot().HaveDependencyOnAny(ModelFrameworks)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public void The_contract_references_nothing_in_the_solution_nor_a_framework()
    {
        var references = References(Contract);

        Assert.DoesNotContain(references, r => r.StartsWith("HitlSendReply", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => ModelFrameworks.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(typeof(AgentApiClient))]
    [InlineData(typeof(HitlSendReply.Web.Components.App))]
    public void The_ui_reaches_the_agent_only_through_the_contract(Type uiType)
    {
        var references = References(uiType.Assembly);

        Assert.DoesNotContain(references, r => r.StartsWith("HitlSendReply", StringComparison.Ordinal)
            && !r.StartsWith("HitlSendReply.Web", StringComparison.Ordinal) && r != Name(Contract) && r != "HitlSendReply.ServiceDefaults");
        Assert.DoesNotContain(references, r => ModelFrameworks.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void The_agent_api_knows_nothing_about_the_mailbox_storage_or_the_ui()
    {
        foreach (var assembly in new[] { Application, Infrastructure, Presentation, Api })
        {
            Assert.DoesNotContain(References(assembly), r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || r.StartsWith("Npgsql", StringComparison.Ordinal) || r.StartsWith("HitlSendReply.Web", StringComparison.Ordinal)
                || r == "MudBlazor");
        }
    }

    [Fact]
    public void The_client_is_built_on_the_contract() =>
        Assert.Contains(Name(Contract), References(typeof(AgentApiClient).Assembly));
}
