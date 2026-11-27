using LabExperiment.Core.Results;
using Microsoft.Extensions.AI;

namespace LabExperiment.Core.Echo;

/// <summary>
/// Sample feature: delete it (and its tests) when the experiment starts. It shows the shape:
/// the code validates and decides, the model only produces text through IChatClient.
/// </summary>
public sealed class EchoAssistant(IChatClient chatClient)
{
    public static readonly Error EmptyQuestion = Error.Validation("Echo.EmptyQuestion", "The question is empty.");

    public async Task<Result<string>> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return Result.Failure<string>(EmptyQuestion);
        }

        ChatResponse response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, "Answer in one sentence."), new ChatMessage(ChatRole.User, question)],
            cancellationToken: cancellationToken);

        return Result.Success(response.Text);
    }
}
