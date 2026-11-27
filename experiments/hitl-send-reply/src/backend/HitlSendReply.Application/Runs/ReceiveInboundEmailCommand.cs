using FluentValidation;
using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

/// <summary>An email that arrived for support. It becomes a run on its ticket.</summary>
public sealed record ReceiveInboundEmailCommand(string EmailId, string From, string Subject, string Body) : ICommand<InboundEmailResult>;

internal sealed class ReceiveInboundEmailCommandValidator : AbstractValidator<ReceiveInboundEmailCommand>
{
    public ReceiveInboundEmailCommandValidator()
    {
        RuleFor(c => c.EmailId).NotEmpty().WithMessage("emailId is required.");
        RuleFor(c => c.From).NotEmpty().WithMessage("from is required.");
    }
}
