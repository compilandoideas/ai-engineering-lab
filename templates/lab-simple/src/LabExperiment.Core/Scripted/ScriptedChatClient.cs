using Microsoft.Extensions.AI;

namespace LabExperiment.Core.Scripted;

/// <summary>
/// Deterministic stand-in for a model: answers with the scripted texts in order and records every
/// request, so the experiment runs and is tested with no provider and no key.
/// </summary>
public sealed class ScriptedChatClient(IReadOnlyList<string> responses) : IChatClient
{
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];

    /// <summary>Every message list sent to the "model", for assertions.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _requests.Add(messages.ToList());

        if (_requests.Count > responses.Count)
        {
            throw new InvalidOperationException($"The script has {responses.Count} responses and request {_requests.Count} was made.");
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responses[_requests.Count - 1])));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
