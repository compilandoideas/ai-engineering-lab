using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

internal sealed class ReviewRunCommandHandler(RunStore runs) : ICommandHandler<ReviewRunCommand, RunDto>
{
    public async Task<Result<RunDto>> Handle(ReviewRunCommand command, CancellationToken cancellationToken)
    {
        if (runs.Find(command.RunId) is not { } entry)
        {
            return Result.Failure<RunDto>(RunErrors.NotFound(command.RunId));
        }

        var reviewer = string.IsNullOrWhiteSpace(command.Reviewer) ? "reviewer" : command.Reviewer;
        var edited = string.IsNullOrWhiteSpace(command.EditedText) ? null : command.EditedText.Trim();
        var verdict = (command.Approved, edited) switch
        {
            (true, not null) => HumanVerdict.Edit(reviewer, edited),
            (true, null) => HumanVerdict.Approve(reviewer),
            _ => HumanVerdict.Reject(reviewer, command.Reason!),
        };

        // A run still starting holds the gate while the agent drafts: answer now instead of waiting.
        if (entry.Run is null)
        {
            return Result.Failure<RunDto>(RunErrors.Starting(entry.Error));
        }

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (entry.Run is not { } run || run.PendingReviews.All(p => p.CallId != command.CallId))
            {
                return Result.Failure<RunDto>(RunErrors.NoPendingReview(command.CallId));
            }

            await entry.Session.Agent.ResumeAsync(run, command.CallId, verdict, cancellationToken);
            return RunMapper.ToDto(entry);
        }
        catch (EditRefusedException refused)
        {
            return Result.Failure<RunDto>(RunErrors.EditRefused(refused.Decision));
        }
        finally
        {
            entry.Gate.Release();
        }
    }
}
