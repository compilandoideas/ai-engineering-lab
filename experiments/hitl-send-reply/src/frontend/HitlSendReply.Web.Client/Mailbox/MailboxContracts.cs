using System.Text.Json.Serialization;

namespace HitlSendReply.Web.Client.Mailbox;

/// <summary>
/// Where an email is in its life: saved, handed to the agent (or not), then what the run did.
/// Notified means the agent accepted it and is drafting; AwaitingReview, Sent, Blocked and
/// RunFailed are read back from the run.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmailStatus>))]
public enum EmailStatus { Received, Notified, NotifyFailed, Unmatched, AwaitingReview, Sent, Blocked, RunFailed }

public static class Mailbox
{
    /// <summary>Every simulated email is addressed to the support inbox.</summary>
    public const string SupportAddress = "support@example.com";
}

/// <summary>The contract between the screen and the Web server's mailbox (not the Agent API).</summary>
public sealed record ComposeEmailRequest(string From, string Subject, string Body);

public sealed record EmailRowDto(
    Guid Id, string From, string To, string Subject, string Body, DateTimeOffset ReceivedAt,
    EmailStatus Status, string? TicketId, string? RunId, string? Detail, EmailReplyDto? Reply = null);

/// <summary>Where a reply that needed a person stands. It starts Pending; the first decision is final.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReplyStatus>))]
public enum ReplyStatus { Pending, Approved, Rejected }

/// <summary>A reply the agent drafted and the policy sent to a person, as kept in email_replies.</summary>
public sealed record EmailReplyDto(
    Guid Id, string CallId, string Recipient, string Text, ReplyStatus Status,
    string? Reason, string? Reviewer, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    string? FinalText = null, bool Edited = false);

public sealed record EmailPageDto(IReadOnlyList<EmailRowDto> Items, int Total);
