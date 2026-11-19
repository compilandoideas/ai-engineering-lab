using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace HitlSendReply.Cli;

/// <summary>
/// Any OpenAI-compatible endpoint: OpenAI, Azure OpenAI (v1 endpoint) or Ollama
/// (LAB_LLM_ENDPOINT=http://localhost:11434/v1, any non-empty key).
/// The agent and the judge use separate models: drafting is cheap, judging is where errors cost.
/// </summary>
public sealed record LlmSettings(Uri? Endpoint, string ApiKey, string AgentModel, string JudgeModel)
{
    public const string DefaultAgentModel = "gpt-4.1-mini";

    /// <summary>
    /// gpt-4.1 by default: in the 100-draft judge study it was stable on 90/90 Spanish drafts with
    /// no escapes, where gpt-4.1-mini missed 4 vague/evasive drafts in every run.
    /// </summary>
    public const string DefaultJudgeModel = "gpt-4.1";

    public static LlmSettings? FromEnvironment()
    {
        var key = Env("LAB_LLM_API_KEY") ?? Env("OPENAI_API_KEY");
        if (key is null)
            return null;

        // LAB_LLM_MODEL (older single setting) applies to both unless a specific one is set.
        var shared = Env("LAB_LLM_MODEL");
        var endpoint = Env("LAB_LLM_ENDPOINT");
        return new LlmSettings(
            endpoint is null ? null : new Uri(endpoint),
            key,
            Env("LAB_AGENT_MODEL") ?? shared ?? DefaultAgentModel,
            Env("LAB_JUDGE_MODEL") ?? shared ?? DefaultJudgeModel);
    }

    public IChatClient CreateChatClient(string model)
    {
        var options = new OpenAIClientOptions();
        if (Endpoint is not null)
            options.Endpoint = Endpoint;

        return new OpenAIClient(new ApiKeyCredential(ApiKey), options).GetChatClient(model).AsIChatClient();
    }

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v && !string.IsNullOrWhiteSpace(v) ? v : null;

    public const string MissingMessage = """
        No LLM configured. Set:
          OPENAI_API_KEY    API key (or LAB_LLM_API_KEY; any non-empty value for Ollama)
          LAB_AGENT_MODEL   optional; default gpt-4.1-mini
          LAB_JUDGE_MODEL   optional; default gpt-4.1
          LAB_LLM_ENDPOINT  optional; e.g. http://localhost:11434/v1 for Ollama
        Nothing was evaluated, so no results were written.
        """;
}
