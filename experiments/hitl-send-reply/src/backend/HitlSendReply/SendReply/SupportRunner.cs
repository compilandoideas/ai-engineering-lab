using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;

namespace HitlSendReply.SendReply;

public sealed record RunResult(string FinalText, int ApprovalRounds);

/// <summary>
/// Runs the agent for one ticket and resolves every approval request the framework surfaces:
/// Block → rejected without a human; NeedsHuman (or anything not auto-approved) → the run stops
/// with a pending review, and <see cref="ResumeAsync"/> continues it with the person's verdict.
/// </summary>
public sealed class SupportRunner(
    AIAgent agent,
    ReplyGate gate,
    ApprovalLedger ledger,
    IReviewHistory reviews,
    IAuditLog audit,
    ITicketStore tickets,
    IHumanApprover? human = null,
    int maxApprovalRounds = 4)
{
    /// <summary>
    /// How approval responses are built. Defaults to the framework's CreateResponse. Tests replace
    /// it to simulate a tampered response (scenario 5).
    /// </summary>
    public Func<ToolApprovalRequestContent, bool, string, AIContent> CreateResponse { get; init; } =
        static (request, approved, reason) => request.CreateResponse(approved, reason);

    /// <summary>
    /// Runs to the end in one call, asking the in-process reviewer at every stop. Used by the
    /// scenarios and tests; an HTTP host uses <see cref="StartAsync"/> and <see cref="ResumeAsync"/>.
    /// </summary>
    public async Task<RunResult> RunAsync(string ticketId, string customerMessage, CancellationToken cancellationToken = default)
    {
        var reviewer = human ?? throw new InvalidOperationException("RunAsync needs an IHumanApprover; use StartAsync/ResumeAsync instead.");
        var run = await StartAsync(ticketId, customerMessage, cancellationToken);

        while (run.Status == ApprovalRunStatus.AwaitingReview)
        {
            var prompt = run.PendingReviews[0];
            var verdict = await reviewer.ReviewAsync(prompt, cancellationToken);
            await ResumeAsync(run, prompt.CallId, verdict, cancellationToken);
        }

        return new RunResult(run.FinalText, run.ApprovalRounds);
    }

    public async Task<ApprovalRun> StartAsync(string ticketId, string customerMessage, CancellationToken cancellationToken = default)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);
        var run = new ApprovalRun(ticketId, session);
        var input = new ChatMessage(ChatRole.User, $"""
            Ticket {ticketId}.
            <customer_message>
            {customerMessage}
            </customer_message>
            """);

        var response = await agent.RunAsync([input], session, cancellationToken: cancellationToken);
        await AdvanceAsync(run, response, cancellationToken);
        return run;
    }

    /// <summary>Records the person's verdict on one pending review and continues the run when nothing else is pending.</summary>
    public async Task<ApprovalRun> ResumeAsync(ApprovalRun run, string callId, HumanVerdict verdict, CancellationToken cancellationToken = default)
    {
        if (!run.TryTakeAwaiting(callId, out var request, out var prompt))
        {
            throw new InvalidOperationException($"No pending review '{callId}' on this run.");
        }

        var reply = SendReplyArguments.TryRead((FunctionCallContent)request.ToolCall!)!;
        audit.Record(verdict.Approved ? "human-approved" : "human-rejected", callId, reply.TicketId, reply.Key.TextHash,
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

        run.Answer(CreateResponse(request, verdict.Approved, verdict.Reason));
        if (run.Status == ApprovalRunStatus.AwaitingReview)
        {
            return run;
        }

        var response = await agent.RunAsync([new ChatMessage(ChatRole.User, run.TakeAnswers())], run.Session, cancellationToken: cancellationToken);
        await AdvanceAsync(run, response, cancellationToken);
        return run;
    }

    // Answers what the policy can answer alone and stops at the first round that needs a person.
    private async Task AdvanceAsync(ApprovalRun run, AgentResponse response, CancellationToken cancellationToken)
    {
        while (run.ApprovalRounds < maxApprovalRounds)
        {
            var pending = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
            if (pending.Count == 0)
            {
                break;
            }

            run.ApprovalRounds++;
            foreach (var request in pending)
            {
                if (await ResolveWithoutHumanAsync(request, cancellationToken) is { } answer)
                {
                    run.Answer(answer);
                }
                else
                {
                    run.Await(request, await PromptForAsync(request, cancellationToken));
                }
            }

            if (run.Status == ApprovalRunStatus.AwaitingReview)
            {
                return;
            }

            response = await agent.RunAsync([new ChatMessage(ChatRole.User, run.TakeAnswers())], run.Session, cancellationToken: cancellationToken);
        }

        run.FinalText = response.Text;
    }

    private async Task<AIContent?> ResolveWithoutHumanAsync(ToolApprovalRequestContent request, CancellationToken cancellationToken)
    {
        if (request.ToolCall is not FunctionCallContent call || SendReplyArguments.TryRead(call) is null)
        {
            return CreateResponse(request, false, "Unsupported tool call.");
        }

        var decision = await gate.EvaluateAsync(call, cancellationToken);
        return decision.Outcome == PolicyOutcome.Block
            ? CreateResponse(request, false, $"Blocked by policy: {decision.Reason}")
            : null; // NeedsHuman, or an AutoSend that surfaced anyway (auto-approval cap reached): a person decides.
    }

    private async Task<ApprovalPrompt> PromptForAsync(ToolApprovalRequestContent request, CancellationToken cancellationToken)
    {
        var call = (FunctionCallContent)request.ToolCall!;
        var reply = SendReplyArguments.TryRead(call)!;
        var decision = await gate.EvaluateAsync(call, cancellationToken); // memoized: the same decision, no second judge call
        var ticket = tickets.Find(reply.TicketId)!;
        return new ApprovalPrompt(call.CallId, reply.TicketId, ticket.CustomerAddress, ticket.Channel, reply.Text, decision);
    }
}
