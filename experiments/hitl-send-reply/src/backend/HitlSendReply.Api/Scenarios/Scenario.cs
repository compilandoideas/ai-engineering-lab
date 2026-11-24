using HitlSendReply.Judge;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Scenarios;

/// <summary>
/// The seven designed cases, in English and Spanish. The agent's drafts and the judge's signals
/// are scripted so the pipeline is tested deterministically; the real judge is measured
/// separately (evaluate).
/// </summary>
public sealed record Scenario(
    int Number,
    string Name,
    Ticket Ticket,
    string CustomerMessage,
    IReadOnlyList<string> Drafts,
    Func<IReplyJudge> Judge,
    PolicyOutcome ExpectedFirstDecision,
    string Expectation,
    string TamperedText = "");
