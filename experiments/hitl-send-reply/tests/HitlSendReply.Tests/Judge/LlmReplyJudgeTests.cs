using Microsoft.Extensions.AI;

namespace HitlSendReply.Tests.Judge;

public class LlmReplyJudgeTests
{
    private sealed class CapturingChatClient(string reply) : IChatClient
    {
        public List<ChatMessage> Seen { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Seen.AddRange(messages);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static readonly Ticket Ticket = SampleTickets.Get("T-1002");

    [Fact]
    public async Task The_judge_sees_the_draft_and_the_order_record_only()
    {
        // The customer's message is not even a parameter of IReplyJudge; this checks what the prompt carries.
        var client = new CapturingChatClient("""{"escalate":false,"category":"None","rationale":"facts only"}""");

        await new LlmReplyJudge(client).EvaluateAsync("Your order #A-1077 was delivered on November 7.", Ticket, default);

        var prompt = string.Join("\n", client.Seen.Select(m => m.Text));
        Assert.Contains("Your order #A-1077 was delivered on November 7.", prompt, StringComparison.Ordinal);
        Assert.Contains("Order #A-1077", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("customer_message", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_clear_verdict_is_parsed()
    {
        var client = new CapturingChatClient("""{"escalate":false,"category":"None","rationale":"facts only"}""");

        var signal = await new LlmReplyJudge(client).EvaluateAsync("Thanks.", Ticket, default);

        Assert.False(signal.Escalate);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task An_unparseable_answer_escalates(string reply)
    {
        var signal = await new LlmReplyJudge(new CapturingChatClient(reply)).EvaluateAsync("Thanks.", Ticket, default);

        Assert.True(signal.Escalate);
    }
}
