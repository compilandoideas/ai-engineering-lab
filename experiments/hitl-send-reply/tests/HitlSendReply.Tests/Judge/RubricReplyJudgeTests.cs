using Microsoft.Extensions.AI;

namespace HitlSendReply.Tests.Judge;

public class RubricReplyJudgeTests
{
    private sealed class FixedChatClient(string reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static readonly Ticket Ticket = SampleTickets.Get("T-1002");

    private const string Nothing = """
        {"money":{"applies":false},"fault":{"applies":false},"deadline":{"applies":false},
         "legalData":{"applies":false},"safety":{"applies":false},"vague":{"applies":false},"offChannel":{"applies":false}}
        """;

    private static Task<JudgeSignal> Judge(string reply, string draft) =>
        new RubricReplyJudge(new FixedChatClient(reply)).EvaluateAsync(draft, Ticket, default);

    [Fact]
    public async Task No_category_clears()
    {
        var signal = await Judge(Nothing, "Thanks.");

        Assert.False(signal.Escalate);
    }

    [Fact]
    public async Task Any_category_escalates_and_the_code_maps_it()
    {
        var reply = Nothing.Replace("\"money\":{\"applies\":false}", "\"money\":{\"applies\":true,\"quote\":\"it's on us\"}");

        var signal = await Judge(reply, "Don't worry, it's on us.");

        Assert.True(signal.Escalate);
        Assert.Equal(JudgeCategory.Commitment, signal.Category);
        Assert.Contains("money: “it's on us”", signal.Rationale);
    }

    [Fact]
    public async Task A_quote_not_in_the_draft_still_escalates_and_is_flagged()
    {
        var reply = Nothing.Replace("\"fault\":{\"applies\":false}", "\"fault\":{\"applies\":true,\"quote\":\"we are to blame\"}");

        var signal = await Judge(reply, "Thanks for writing.");

        Assert.True(signal.Escalate);
        Assert.Contains("quote not found", signal.Rationale);
    }

    [Fact]
    public async Task A_missing_category_counts_as_applying()
    {
        var signal = await Judge("""{"money":{"applies":false}}""", "Thanks.");

        Assert.True(signal.Escalate);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public async Task An_unparseable_answer_escalates(string reply)
    {
        var signal = await Judge(reply, "Thanks.");

        Assert.True(signal.Escalate);
    }
}
