using Microsoft.Extensions.AI;

namespace HitlSendReply.Tests.Fakes;

/// <summary>Holds every model call until the test releases it, or fails it: a slow or broken model.</summary>
public sealed class ControlledChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _gate.TrySetResult();

    public void Fail(Exception exception) => _gate.TrySetException(exception);

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        await _gate.Task.WaitAsync(cancellationToken);
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _gate.Task.WaitAsync(cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }
}
