using LabExperiment.Core.Echo;
using LabExperiment.Core.Scripted;

namespace LabExperiment.Tests;

// Sample tests: delete them with the sample feature.
public class EchoAssistantTests
{
    [Fact]
    public async Task Returns_the_model_text_and_sends_the_question()
    {
        var model = new ScriptedChatClient(["Yes."]);

        var result = await new EchoAssistant(model).AskAsync("Is it wired?");

        Assert.True(result.IsSuccess);
        Assert.Equal("Yes.", result.Value);
        Assert.Equal("Is it wired?", model.Requests.Single().Last().Text);
    }

    [Fact]
    public async Task An_empty_question_is_a_validation_error_and_never_reaches_the_model()
    {
        var model = new ScriptedChatClient([]);

        var result = await new EchoAssistant(model).AskAsync("  ");

        Assert.True(result.IsFailure);
        Assert.Equal(EchoAssistant.EmptyQuestion, result.Error);
        Assert.Empty(model.Requests);
    }
}
