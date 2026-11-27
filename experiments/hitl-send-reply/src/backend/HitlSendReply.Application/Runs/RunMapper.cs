using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

/// <summary>The system's types never cross the HTTP boundary; only the contract does.</summary>
public static class RunMapper
{
    public static RunDto ToDto(RunEntry entry)
    {
        if (entry.Run is not { } run)
        {
            return new RunDto(entry.Id, entry.Session.Mode, entry.Session.Scenario, entry.TicketId, entry.CustomerMessage,
                entry.Error is null ? RunStatus.Starting : RunStatus.Failed, [], [], Audit(entry), "", entry.Error);
        }

        return new RunDto(
            entry.Id,
            entry.Session.Mode,
            entry.Session.Scenario,
            run.TicketId,
            entry.CustomerMessage,
            run.Status == ReplyRunStatus.AwaitingReview ? RunStatus.AwaitingReview : RunStatus.Completed,
            run.PendingReviews.Select(p => new PendingReviewDto(p.CallId, p.TicketId, p.Recipient, p.Channel.ToString(), p.Text,
                new DecisionDto(p.Decision.Outcome.ToString(), p.Decision.Stage.ToString(), p.Decision.Reason))).ToList(),
            entry.Session.Outbox.Sent.Select(s => new SentReplyDto(s.Key.TicketId, s.Recipient, s.Channel.ToString(), s.Text, s.Key.TextHash, s.SentAt)).ToList(),
            Audit(entry),
            run.FinalText);
    }

    private static List<AuditEntryDto> Audit(RunEntry entry) =>
        entry.Session.Audit.Entries.Select(a => new AuditEntryDto(a.At, a.Event, a.CallId, a.TicketId, a.TextHash, a.Recipient,
            a.Channel, a.Outcome, a.Stage, a.Reason, a.Actor)).ToList();
}
