using HitlSendReply.Contracts;
using HitlSendReply.SendReply;

namespace HitlSendReply.Api.Runs;

/// <summary>The system's types never cross the HTTP boundary; only the contract does.</summary>
public static class RunMapper
{
    public static RunDto ToDto(RunEntry entry) => new(
        entry.Id,
        entry.Mode,
        entry.Scenario,
        entry.Run.TicketId,
        entry.CustomerMessage,
        entry.Run.Status == ApprovalRunStatus.AwaitingReview ? RunStatus.AwaitingReview : RunStatus.Completed,
        entry.Run.PendingReviews.Select(p => new PendingReviewDto(p.CallId, p.TicketId, p.Recipient, p.Channel.ToString(), p.Text,
            new DecisionDto(p.Decision.Outcome.ToString(), p.Decision.Stage.ToString(), p.Decision.Reason))).ToList(),
        entry.Pipeline.Outbox.Sent.Select(s => new SentReplyDto(s.Key.TicketId, s.Recipient, s.Channel.ToString(), s.Text, s.Key.TextHash, s.SentAt)).ToList(),
        entry.Pipeline.Audit.Entries.Select(a => new AuditEntryDto(a.At, a.Event, a.CallId, a.TicketId, a.TextHash, a.Recipient,
            a.Channel, a.Outcome, a.Stage, a.Reason, a.Actor)).ToList(),
        entry.Run.FinalText);
}
