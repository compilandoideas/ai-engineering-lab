using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Mailbox;

/// <summary>One simulated inbound email. Its Id is the EmailId the webhook is idempotent on.</summary>
public sealed class InboundEmail
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string From { get; init; }
    public string To { get; init; } = Client.Mailbox.Mailbox.SupportAddress;
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public EmailStatus Status { get; set; } = EmailStatus.Received;
    public string? TicketId { get; set; }
    public string? RunId { get; set; }

    /// <summary>Why the email is where it is: the notify error, or why it was unmatched.</summary>
    public string? Detail { get; set; }

    public EmailRowDto ToDto() => new(Id, From, To, Subject, Body, ReceivedAt, Status, TicketId, RunId, Detail);
}
