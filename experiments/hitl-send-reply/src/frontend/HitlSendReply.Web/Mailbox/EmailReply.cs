using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// A reply waiting for a person, kept in Postgres while the run waits. One row per review the
/// policy asked for (CallId): a rejection makes the agent rewrite, and the rewrite is a new row.
/// </summary>
public sealed class EmailReply
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EmailId { get; init; }
    public required string RunId { get; init; }
    public required string CallId { get; init; }
    public required string Recipient { get; init; }
    /// <summary>The agent's draft, as the person saw it.</summary>
    public required string Text { get; init; }

    /// <summary>What reached the customer: the draft, or the person's edited version.</summary>
    public string? FinalText { get; private set; }

    public bool Edited { get; private set; }
    public ReplyStatus Status { get; private set; } = ReplyStatus.Pending;
    public string? Reason { get; private set; }
    public string? Reviewer { get; private set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Only a pending reply takes a decision: the first one is final. False when it was already decided.</summary>
    public bool Decide(bool approved, string? reason, string? reviewer, DateTimeOffset at, bool edited = false, string? finalText = null)
    {
        if (Status != ReplyStatus.Pending)
        {
            return false;
        }

        Status = approved ? ReplyStatus.Approved : ReplyStatus.Rejected;
        Reason = reason;
        Reviewer = reviewer;
        DecidedAt = at;
        Edited = edited;
        FinalText = approved ? finalText ?? Text : null;
        return true;
    }

    public EmailReplyDto ToDto() => new(Id, CallId, Recipient, Text, Status, Reason, Reviewer, CreatedAt, DecidedAt, FinalText, Edited);
}
