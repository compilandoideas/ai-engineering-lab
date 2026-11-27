using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Abstractions;

/// <summary>
/// One isolated instance of the system for one run: its agent and what it delivered and recorded.
/// Mode says whether a real model drafts (Live) or a designed case plays the agent (Scenario).
/// </summary>
public sealed record RunSession(IReplyAgent Agent, IOutbox Outbox, IAuditLog Audit, RunMode Mode, int? Scenario = null);

/// <summary>Builds the session for a ticket: with a model configured the real agent, otherwise the scripted demo.</summary>
public interface IRunSessionFactory
{
    RunSession Create(Ticket ticket);
}
