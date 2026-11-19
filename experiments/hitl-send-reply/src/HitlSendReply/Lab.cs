using HitlSendReply.Agent;
using HitlSendReply.Domain;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.Publishing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HitlSendReply;

/// <summary>Composition root: wires one isolated instance of the whole pipeline.</summary>
public sealed class Lab
{
    public required ITicketStore Tickets { get; init; }
    public required FakeOutbox Outbox { get; init; }
    public required ApprovalLedger Ledger { get; init; }
    public required AuditLog Audit { get; init; }
    public required PublishPolicy Policy { get; init; }
    public required ReplyGate Gate { get; init; }
    public required SupportTools Tools { get; init; }
    public required AIAgent Agent { get; init; }
    public required SupportRunner Runner { get; init; }

    public static Lab Create(
        IChatClient agentModel,
        IReplyJudge judge,
        IHumanApprover human,
        TimeSpan? judgeTimeout = null,
        string? auditPath = null,
        TimeProvider? clock = null,
        Func<ToolApprovalRequestContent, bool, string, AIContent>? createResponse = null,
        bool disableApprovalResponseBinding = false)
    {
        clock ??= TimeProvider.System;
        var tickets = SampleTickets.Store();
        var outbox = new FakeOutbox(clock);
        var ledger = new ApprovalLedger();
        var audit = new AuditLog(clock, auditPath);
        var policy = new PublishPolicy(tickets, AgentPermissions.EmailOnly(SupportAgentFactory.AgentName), outbox, judge,
            new PublishPolicyOptions(SupportAgentFactory.AgentName, judgeTimeout ?? TimeSpan.FromSeconds(20)));
        var gate = new ReplyGate(policy, ledger, audit, tickets);
        var tools = new SupportTools(tickets, outbox, ledger, audit);
        var agent = SupportAgentFactory.Create(agentModel, tools, gate, disableApprovalResponseBinding);
        var runner = createResponse is null
            ? new SupportRunner(agent, gate, human, ledger, audit, tickets)
            : new SupportRunner(agent, gate, human, ledger, audit, tickets) { CreateResponse = createResponse };

        return new Lab
        {
            Tickets = tickets, Outbox = outbox, Ledger = ledger, Audit = audit, Policy = policy,
            Gate = gate, Tools = tools, Agent = agent, Runner = runner,
        };
    }
}
