namespace HitlSendReply.Application.Review;

/// <summary>What the agent is told about its proposed send after a person decided.</summary>
public sealed record AgentAnswer(bool Approved, string Reason);

/// <summary>
/// A person's verdict on one pending review: approve, edit or reject. It records the decision,
/// authorizes the exact text a person approved and, for an edit, sends the person's version. The
/// agent adapter only relays the returned <see cref="AgentAnswer"/> to the agent.
/// </summary>
public sealed class HumanReview(ReplyGate gate, ApprovalLedger ledger, IReviewHistory reviews, IAuditLog audit, ReplySender sender)
{
    /// <summary>
    /// Turns an edit that changes nothing into an approval, and refuses an edit that breaks a
    /// deterministic rule. Called before the review is taken, so a refused edit leaves it pending.
    /// </summary>
    public HumanVerdict Check(ApprovalPrompt prompt, HumanVerdict verdict)
    {
        if (verdict.EditedText is not { } edited)
        {
            return verdict;
        }

        if (edited == prompt.Text)
        {
            return HumanVerdict.Approve(verdict.Reviewer);
        }

        return gate.CheckHumanEdit(new ReplyRequest(prompt.TicketId, edited)) is { } refused
            ? throw new EditRefusedException(refused)
            : verdict;
    }

    /// <summary>Applies a checked verdict and returns what the agent must be told.</summary>
    public AgentAnswer Apply(ApprovalPrompt prompt, HumanVerdict verdict)
    {
        var reply = new ReplyRequest(prompt.TicketId, prompt.Text);
        if (verdict.EditedText is { } text)
        {
            return new AgentAnswer(false, SendEdit(prompt, reply, verdict, text));
        }

        audit.Record(verdict.Approved ? "human-approved" : "human-rejected", prompt.CallId, reply.TicketId, reply.Key.TextHash,
            prompt.Recipient, prompt.Channel.ToString(), verdict.Approved ? "Approved" : "Rejected",
            prompt.Decision.Stage.ToString(), verdict.Reason, verdict.Reviewer);

        if (verdict.Approved)
        {
            ledger.Authorize(reply.Key, verdict.Reviewer);
        }
        else
        {
            reviews.RecordRejection(reply.Key, verdict.Reason);
        }

        return new AgentAnswer(verdict.Approved, verdict.Reason);
    }

    // The agent framework replays the agent's own call on approval, never edited arguments, so the
    // reviewer's text is sent here through the same send path, and the agent's call is answered as
    // replaced. Its draft counts as rejected: any later draft on this ticket goes to a person too.
    private string SendEdit(ApprovalPrompt prompt, ReplyRequest reply, HumanVerdict verdict, string text)
    {
        var edited = new ReplyRequest(reply.TicketId, text);
        audit.Record("human-edited", prompt.CallId, reply.TicketId, edited.Key.TextHash, prompt.Recipient, prompt.Channel.ToString(),
            "Edited", prompt.Decision.Stage.ToString(), verdict.Reason, verdict.Reviewer);
        reviews.RecordRejection(reply.Key, "Replaced by the reviewer's edited text.");
        ledger.Authorize(edited.Key, verdict.Reviewer);
        var result = sender.Send(reply.TicketId, text, prompt.CallId);
        return $"The reviewer edited your reply and sent their version ({result}). Do not send another reply.";
    }
}
