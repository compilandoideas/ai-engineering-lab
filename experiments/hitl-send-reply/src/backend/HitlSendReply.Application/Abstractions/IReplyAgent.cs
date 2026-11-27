namespace HitlSendReply.Application.Abstractions;

public enum ReplyRunStatus { AwaitingReview, Completed }

/// <summary>One agent run on one ticket. It can stop while a person decides and continue in another request.</summary>
public interface IReplyRun
{
    string TicketId { get; }

    ReplyRunStatus Status { get; }

    /// <summary>What a person must decide before the run can continue.</summary>
    IReadOnlyList<ApprovalPrompt> PendingReviews { get; }

    /// <summary>The agent's last message to the operator (not the reply to the customer).</summary>
    string FinalText { get; }

    int ApprovalRounds { get; }
}

/// <summary>
/// The agent that drafts replies and proposes send_reply. The application never sees how it is
/// built (Microsoft Agent Framework today): it starts a run, and resumes it with a person's verdict.
/// </summary>
public interface IReplyAgent
{
    Task<IReplyRun> StartAsync(string ticketId, string customerMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the verdict on one pending review and continues when nothing else is pending.
    /// Throws <see cref="Review.EditRefusedException"/> when an edited text breaks a deterministic rule.
    /// </summary>
    Task<IReplyRun> ResumeAsync(IReplyRun run, string callId, HumanVerdict verdict, CancellationToken cancellationToken = default);
}
