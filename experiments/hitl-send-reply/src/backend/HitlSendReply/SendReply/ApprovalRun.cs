using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HitlSendReply.SendReply;

public enum ApprovalRunStatus { AwaitingReview, Completed }

/// <summary>
/// One agent run on one ticket that can stop while a person decides and continue later, in another
/// request. It keeps the framework session and the answers already given in the current round.
/// Not thread-safe: the caller serializes access to a run.
/// </summary>
public sealed class ApprovalRun
{
    private readonly Dictionary<string, (ToolApprovalRequestContent Request, ApprovalPrompt Prompt)> _awaiting = [];
    private readonly List<AIContent> _answers = [];

    internal ApprovalRun(string ticketId, AgentSession session)
    {
        TicketId = ticketId;
        Session = session;
    }

    public string TicketId { get; }

    public ApprovalRunStatus Status => _awaiting.Count > 0 ? ApprovalRunStatus.AwaitingReview : ApprovalRunStatus.Completed;

    /// <summary>What a person must decide before the run can continue.</summary>
    public IReadOnlyList<ApprovalPrompt> PendingReviews => _awaiting.Values.Select(a => a.Prompt).ToList();

    /// <summary>The agent's last message to the operator (not the reply to the customer).</summary>
    public string FinalText { get; internal set; } = "";

    public int ApprovalRounds { get; internal set; }

    internal AgentSession Session { get; }

    internal void Await(ToolApprovalRequestContent request, ApprovalPrompt prompt) => _awaiting[prompt.CallId] = (request, prompt);

    internal bool TryTakeAwaiting(string callId, out ToolApprovalRequestContent request, out ApprovalPrompt prompt)
    {
        if (_awaiting.Remove(callId, out var entry))
        {
            (request, prompt) = entry;
            return true;
        }

        request = null!;
        prompt = null!;
        return false;
    }

    internal void Answer(AIContent answer) => _answers.Add(answer);

    internal List<AIContent> TakeAnswers()
    {
        var answers = _answers.ToList();
        _answers.Clear();
        return answers;
    }
}
