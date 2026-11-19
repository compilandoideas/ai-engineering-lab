using HitlSendReply.Policy;
using NetArchTest.Rules;

namespace HitlSendReply.Tests;

/// <summary>
/// The decision logic must not depend on the agent framework or on the LLM abstraction.
/// MAF is an adapter (Agent/); the LLM judge is an adapter (Judge/).
/// </summary>
public class ArchitectureTests
{
    private static readonly string[] Frameworks =
    [
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "OpenAI",
        "Spectre.Console",
    ];

    [Theory]
    [InlineData("HitlSendReply.Domain")]
    [InlineData("HitlSendReply.Policy")]
    public void Core_does_not_depend_on_agent_framework_llm_or_ui(string @namespace)
    {
        var result = Types.InAssembly(typeof(PublishPolicy).Assembly)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOnAny(Frameworks)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{@namespace} depends on a framework: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [InlineData("HitlSendReply.Domain")]
    [InlineData("HitlSendReply.Policy")]
    public void Core_does_not_depend_on_adapters(string @namespace)
    {
        var result = Types.InAssembly(typeof(PublishPolicy).Assembly)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOnAny(
                "HitlSendReply.Agent", "HitlSendReply.Judge", "HitlSendReply.Publishing", "HitlSendReply.Cli")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{@namespace} depends on an adapter: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void The_rule_detects_a_violation()
    {
        // Guard against a rule that passes vacuously: the Agent adapter *does* depend on MAF.
        var result = Types.InAssembly(typeof(PublishPolicy).Assembly)
            .That().ResideInNamespace("HitlSendReply.Agent")
            .ShouldNot().HaveDependencyOnAny(Frameworks)
            .GetResult();

        Assert.False(result.IsSuccessful);
    }
}
