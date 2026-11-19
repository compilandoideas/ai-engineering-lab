using HitlSendReply.Lab;
using HitlSendReply.Policy;
using NetArchTest.Rules;

namespace HitlSendReply.Tests;

/// <summary>
/// Two boundaries. The system (HitlSendReply) never knows about the lab around it. Inside the
/// system, the decision (Tickets, Policy) never knows about the agent framework or the LLM:
/// MAF lives in SendReply/, the LLM judge in Judge/.
/// </summary>
public class LayeringTests
{
    private static readonly System.Reflection.Assembly SystemAssembly = typeof(PublishPolicy).Assembly;

    private static readonly string[] Frameworks =
    [
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "OpenAI",
        "Spectre.Console",
    ];

    [Fact]
    public void The_system_does_not_reference_the_lab_the_console_or_a_model_provider()
    {
        var references = SystemAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain(typeof(Pipeline).Assembly.GetName().Name, references);
        Assert.DoesNotContain("Spectre.Console", references);
        Assert.DoesNotContain("OpenAI", references);
    }

    [Theory]
    [InlineData("HitlSendReply.Tickets")]
    [InlineData("HitlSendReply.Policy")]
    public void The_decision_does_not_depend_on_the_agent_framework_or_the_llm(string @namespace)
    {
        var result = Types.InAssembly(SystemAssembly)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOnAny(Frameworks)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{@namespace} depends on a framework: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [InlineData("HitlSendReply.Tickets")]
    [InlineData("HitlSendReply.Policy")]
    public void The_decision_does_not_depend_on_the_agent_adapter(string @namespace)
    {
        var result = Types.InAssembly(SystemAssembly)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOn("HitlSendReply.SendReply")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{@namespace} depends on SendReply: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Tickets_depend_on_nothing_else_in_the_system()
    {
        var result = Types.InAssembly(SystemAssembly)
            .That().ResideInNamespace("HitlSendReply.Tickets")
            .ShouldNot().HaveDependencyOnAny("HitlSendReply.Policy", "HitlSendReply.Judge", "HitlSendReply.SendReply")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Tickets depends on: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void The_rule_detects_a_violation()
    {
        // Guard against a rule that passes vacuously: the SendReply adapter *does* depend on MAF.
        var result = Types.InAssembly(SystemAssembly)
            .That().ResideInNamespace("HitlSendReply.SendReply")
            .ShouldNot().HaveDependencyOnAny(Frameworks)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }
}
