using HitlSendReply.Contracts;

namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// Hands a saved email to the agent. Today an HTTP webhook; later a queue publisher whose
/// consumer calls the same inbound handler. Throws when the agent could not be reached.
/// </summary>
public interface IInboundEmailNotifier
{
    Task<InboundEmailResult> NotifyAsync(InboundEmail email, CancellationToken cancellationToken);
}
