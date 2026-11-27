using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using HitlSendReply.Contracts;

namespace HitlSendReply.Infrastructure;

/// <summary>
/// Wires one isolated instance of the system for one run: in-memory stores, the policy, the review
/// rules and the agent on Microsoft Agent Framework. Exposes what the API and the tests look at.
/// </summary>
public sealed class Pipeline
{
    public required ITicketStore Tickets { get; init; }
    public required IOutbox Outbox { get; init; }
    public required IAuditLog Audit { get; init; }
    public required SupportTools Tools { get; init; }
    public required SupportRunner Runner { get; init; }

    /// <summary>The run as the application sees it: the agent behind its port and what it recorded.</summary>
    public RunSession Session(RunMode mode, int? scenario = null) => new(Runner, Outbox, Audit, mode, scenario);

    public static Pipeline Create(
        IChatClient agentModel,
        IReplyJudge judge,
        IHumanApprover? human,
        Language language = Language.En,
        TimeSpan? judgeTimeout = null,
        string? auditPath = null,
        TimeProvider? clock = null,
        Func<ToolApprovalRequestContent, bool, string, AIContent>? createResponse = null,
        bool disableApprovalResponseBinding = false)
    {
        clock ??= TimeProvider.System;
        var tickets = SampleTickets.Store(language);
        var outbox = new InMemoryOutbox(clock);
        var ledger = new ApprovalLedger();
        var audit = new InMemoryAuditLog(clock, auditPath);
        var reviews = new InMemoryReviewHistory();
        var policy = new PublishPolicy(tickets, AgentPermissions.EmailOnly(SupportAgent.AgentName), outbox, judge,
            new PublishPolicyOptions(SupportAgent.AgentName, judgeTimeout ?? TimeSpan.FromSeconds(20)), reviews);
        var gate = new ReplyGate(policy, ledger, audit, tickets);
        var sender = new ReplySender(tickets, outbox, ledger, audit);
        var review = new HumanReview(gate, ledger, reviews, audit, sender);
        var tools = new SupportTools(tickets, sender);
        var agent = SupportAgent.Create(agentModel, tools, gate, disableApprovalResponseBinding);
        var runner = createResponse is null
            ? new SupportRunner(agent, gate, review, tickets, human)
            : new SupportRunner(agent, gate, review, tickets, human) { CreateResponse = createResponse };

        return new Pipeline
        {
            Tickets = tickets, Outbox = outbox, Audit = audit, Tools = tools, Runner = runner,
        };
    }
}
