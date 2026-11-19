using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace HitlSendReply.Cli;

/// <summary>
/// Any OpenAI-compatible endpoint: OpenAI, Azure OpenAI (v1 endpoint) or Ollama
/// (LAB_LLM_ENDPOINT=http://localhost:11434/v1, any non-empty key).
/// </summary>
public sealed record LlmSettings(Uri? Endpoint, string ApiKey, string Model)
{
    public static LlmSettings? FromEnvironment()
    {
        var key = Environment.GetEnvironmentVariable("LAB_LLM_API_KEY");
        var model = Environment.GetEnvironmentVariable("LAB_LLM_MODEL");
        var endpoint = Environment.GetEnvironmentVariable("LAB_LLM_ENDPOINT");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model))
            return null;

        return new LlmSettings(string.IsNullOrWhiteSpace(endpoint) ? null : new Uri(endpoint), key, model);
    }

    public IChatClient CreateChatClient()
    {
        var options = new OpenAIClientOptions();
        if (Endpoint is not null)
            options.Endpoint = Endpoint;

        return new OpenAIClient(new ApiKeyCredential(ApiKey), options).GetChatClient(Model).AsIChatClient();
    }

    public const string MissingMessage = """
        No LLM configured. Set:
          LAB_LLM_API_KEY   API key (any non-empty value for Ollama)
          LAB_LLM_MODEL     model name, e.g. gpt-4.1-mini or llama3.1:8b
          LAB_LLM_ENDPOINT  optional; e.g. http://localhost:11434/v1 for Ollama
        Nothing was evaluated, so no results were written.
        """;
}
