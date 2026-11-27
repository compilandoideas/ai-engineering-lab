using Microsoft.Extensions.AI;

namespace HitlSendReply.Infrastructure.Llm;

/// <summary>
/// The real agent and judge models, created once per process. Null settings mean no key is
/// configured: live runs are refused with 409 and scenarios still work.
/// </summary>
public sealed class LiveModels(LlmSettings? settings) : IDisposable
{
    private readonly Lazy<IChatClient>? _agent = settings is null ? null : new(() => settings!.CreateChatClient(settings.AgentModel));
    private readonly Lazy<IChatClient>? _judge = settings is null ? null : new(() => settings!.CreateChatClient(settings.JudgeModel));

    public bool IsConfigured => settings is not null;

    public IChatClient Agent => (_agent ?? throw new InvalidOperationException(LlmSettings.MissingMessage)).Value;

    public IReplyJudge CreateJudge() => settings!.CreateJudge((_judge ?? throw new InvalidOperationException(LlmSettings.MissingMessage)).Value);

    public void Dispose()
    {
        if (_agent is { IsValueCreated: true })
        {
            _agent.Value.Dispose();
        }

        if (_judge is { IsValueCreated: true })
        {
            _judge.Value.Dispose();
        }
    }
}
