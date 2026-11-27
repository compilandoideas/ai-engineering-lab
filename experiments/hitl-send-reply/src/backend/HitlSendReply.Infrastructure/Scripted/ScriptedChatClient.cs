using Microsoft.Extensions.AI;

namespace HitlSendReply.Infrastructure.Scripted;

/// <summary>
/// Deterministic stand-in for the model, so the approval pipeline can be tested without an LLM.
/// It emits the scripted send_reply drafts in order: the first after the customer message, the
/// next one each time a send is rejected. It never decides anything about safety.
/// </summary>
public sealed class ScriptedChatClient(string ticketId, IReadOnlyList<string> drafts) : IChatClient
{
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];

    /// <summary>Every message list the agent sent to the "model", for assertions.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var history = messages.ToList();
        _requests.Add(history);

        var draftsUsed = history.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Count();
        var lastResult = history.LastOrDefault()?.Contents.OfType<FunctionResultContent>().LastOrDefault();
        var lastResultText = lastResult?.Result?.ToString() ?? "";

        var sentOrStopped = lastResult is not null && lastResultText.StartsWith("SENT", StringComparison.Ordinal);
        if (sentOrStopped || draftsUsed >= drafts.Count)
        {
            var summary = lastResult is null ? "No reply sent." : $"Last tool result: {lastResultText}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, summary)));
        }

        var call = new FunctionCallContent($"call-{draftsUsed + 1}-{Guid.NewGuid():N}", SendReplyArguments.ToolName, new Dictionary<string, object?>
        {
            ["ticketId"] = ticketId,
            ["text"] = drafts[draftsUsed],
        });

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
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
