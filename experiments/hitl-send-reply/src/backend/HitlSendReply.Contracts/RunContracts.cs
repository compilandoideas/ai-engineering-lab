using System.Text.Json.Serialization;

namespace HitlSendReply.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<RunMode>))]
public enum RunMode { Scenario, Live }

[JsonConverter(typeof(JsonStringEnumConverter<RunStatus>))]
public enum RunStatus { AwaitingReview, Completed, Starting, Failed }

/// <summary>Start a run: a designed scenario (scripted model and judge, no key) or the real model on a ticket.</summary>
public sealed record StartRunRequest(RunMode Mode, int? Scenario = null, string? TicketId = null, string? CustomerMessage = null, string Lang = "en");

/// <summary>
/// A person's verdict on one pending review. A rejection needs a reason: the agent reads it to
/// rewrite. EditedText approves the reviewer's own version, which passes the deterministic rules again.
/// </summary>
public sealed record ReviewRequest(bool Approved, string? Reason, string Reviewer, string? EditedText = null);

/// <summary>What the policy decided and why. Outcome: Block, NeedsHuman or AutoSend.</summary>
public sealed record DecisionDto(string Outcome, string Stage, string Reason);

/// <summary>What the reviewer must see: the exact text, where it goes (from the ticket) and why it stopped.</summary>
public sealed record PendingReviewDto(string CallId, string TicketId, string Recipient, string Channel, string Text, DecisionDto Decision);

public sealed record SentReplyDto(string TicketId, string Recipient, string Channel, string Text, string TextHash, DateTimeOffset SentAt);

public sealed record AuditEntryDto(
    DateTimeOffset At, string Event, string? CallId, string TicketId, string TextHash,
    string? Recipient, string? Channel, string? Outcome, string? Stage, string? Reason, string? Actor);

public sealed record RunDto(
    string RunId,
    RunMode Mode,
    int? Scenario,
    string TicketId,
    string CustomerMessage,
    RunStatus Status,
    IReadOnlyList<PendingReviewDto> PendingReviews,
    IReadOnlyList<SentReplyDto> Sent,
    IReadOnlyList<AuditEntryDto> Audit,
    string AgentReply,
    string? Error = null);
