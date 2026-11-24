using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using HitlSendReply.Api.InMemory;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.SendReply;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Runs;

/// <summary>
/// Composition root: wires one isolated instance of the system with in-memory fakes around it.
/// Exposes only what the lab and the tests look at afterwards.
/// </summary>
public sealed class Pipeline
{
    public required ITicketStore Tickets { get; init; }
    public required IOutbox Outbox { get; init; }
    public required IAuditLog Audit { get; init; }
    public required SupportTools Tools { get; init; }
    public required SupportRunner Runner { get; init; }

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
        var tools = new SupportTools(tickets, outbox, ledger, audit);
        var agent = SupportAgent.Create(agentModel, tools, gate, disableApprovalResponseBinding);
        var runner = createResponse is null
            ? new SupportRunner(agent, gate, ledger, reviews, audit, tickets, human)
            : new SupportRunner(agent, gate, ledger, reviews, audit, tickets, human) { CreateResponse = createResponse };

        return new Pipeline
        {
            Tickets = tickets, Outbox = outbox, Audit = audit, Tools = tools, Runner = runner,
        };
    }
}
