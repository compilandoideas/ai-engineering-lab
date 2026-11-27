using FluentValidation;
using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

/// <summary>A person's verdict on one pending review: approve, approve an edited text, or reject with a reason.</summary>
public sealed record ReviewRunCommand(string RunId, string CallId, bool Approved, string? Reason, string? EditedText, string? Reviewer)
    : ICommand<RunDto>;

internal sealed class ReviewRunCommandValidator : AbstractValidator<ReviewRunCommand>
{
    public ReviewRunCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().When(c => !c.Approved)
            .WithMessage("A rejection needs a reason: the agent reads it to rewrite.");
        RuleFor(c => c.EditedText).Empty().When(c => !c.Approved)
            .WithMessage("An edited text is sent on approval: set approved to true.");
    }
}
