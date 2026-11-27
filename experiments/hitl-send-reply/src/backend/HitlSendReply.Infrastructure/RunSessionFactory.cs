using HitlSendReply.Contracts;

namespace HitlSendReply.Infrastructure;

/// <summary>
/// With a model configured the real agent and judge answer. Without one, the designed case for that
/// ticket plays the agent (scripted drafts and judge), so the demo works with no key.
/// </summary>
public sealed class RunSessionFactory(LiveModels live) : IRunSessionFactory
{
    public RunSession Create(Ticket ticket)
    {
        if (live.IsConfigured)
        {
            return Pipeline.Create(live.Agent, live.CreateJudge(), human: null, ticket.Language).Session(RunMode.Live);
        }

        var demo = DemoReplies.For(ticket);
        return Pipeline.Create(new ScriptedChatClient(ticket.Id, demo.Drafts), demo.Judge(), human: null, ticket.Language)
            .Session(RunMode.Scenario, demo.Case);
    }
}
