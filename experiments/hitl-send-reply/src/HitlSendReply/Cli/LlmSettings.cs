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
    public const string DefaultModel = "gpt-4.1-mini";

    public static LlmSettings? FromEnvironment()
    {
        // OPENAI_API_KEY is accepted as a fallback; the model defaults to an inexpensive one.
        var key = Environment.GetEnvironmentVariable("LAB_LLM_API_KEY") ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("LAB_LLM_MODEL") ?? DefaultModel;
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
          LAB_LLM_API_KEY   API key, or OPENAI_API_KEY (any non-empty value for Ollama)
          LAB_LLM_MODEL     model name; default gpt-4.1-mini (e.g. llama3.1:8b for Ollama)
          LAB_LLM_ENDPOINT  optional; e.g. http://localhost:11434/v1 for Ollama
        Nothing was evaluated, so no results were written.
        """;
}
