using System.Text.Json.Serialization;
using System.Text.Json;

namespace HitlSendReply.Evals;

public sealed record LabeledDraft(string Id, string TicketId, string Text, PolicyOutcome Expected, string Note);

public sealed record ItemResult(string Id, PolicyOutcome Expected, PolicyOutcome Actual, PolicyStage Stage, string Reason, string Note);

public sealed record RunReport(
    int Run,
    int Total,
    int Correct,
    int FalseNegatives,
    int FalsePositives,
    int JudgeReached,
    int JudgeMisses,
    IReadOnlyList<ItemResult> Items);

public sealed record EvaluationReport(string Model, string Language, DateTimeOffset At, IReadOnlyList<RunReport> Runs);

/// <summary>
/// Runs the full policy (with the given judge) over a hand-labeled set of drafts.
/// False negative = a draft labeled NeedsHuman/Block that came out AutoSend (the costly error).
/// False positive = a draft labeled AutoSend that went to a human or was blocked.
/// </summary>
public static class Evaluator
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary><c>dev</c>: the 100 drafts used while building the judge. <c>blind</c>: the held-out set, never used to tune.</summary>
    public static string DatasetPath(Language language, string set = "dev") =>
        Path.Combine(AppContext.BaseDirectory, "data",
            $"{(set == "blind" ? "blind" : "drafts")}.{language.ToString().ToLowerInvariant()}.jsonl");

    public static IReadOnlyList<LabeledDraft> Load(string path) =>
        File.ReadLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<LabeledDraft>(l, Json)!)
            .ToList();

    public static async Task<RunReport> RunOnceAsync(int run, IReadOnlyList<LabeledDraft> drafts, IReplyJudge judge,
        TimeSpan judgeTimeout, Language language = Language.En, CancellationToken cancellationToken = default)
    {
        var results = new List<ItemResult>();
        foreach (var draft in drafts)
        {
            // Fresh state per draft so one item cannot influence another (e.g. via duplicates).
            var policy = new PublishPolicy(
                SampleTickets.Store(language),
                AgentPermissions.EmailOnly(SupportAgent.AgentName),
                new InMemoryOutbox(TimeProvider.System),
                judge,
                new PublishPolicyOptions(SupportAgent.AgentName, judgeTimeout));

            var decision = await policy.EvaluateAsync(new ReplyRequest(draft.TicketId, draft.Text), cancellationToken);
            results.Add(new ItemResult(draft.Id, draft.Expected, decision.Outcome, decision.Stage, decision.Reason, draft.Note));
        }

        var judgeReached = results.Where(r => r.Stage == PolicyStage.Judge).ToList();
        return new RunReport(
            run,
            results.Count,
            results.Count(r => r.Actual == r.Expected),
            results.Count(r => r.Expected != PolicyOutcome.AutoSend && r.Actual == PolicyOutcome.AutoSend),
            results.Count(r => r.Expected == PolicyOutcome.AutoSend && r.Actual != PolicyOutcome.AutoSend),
            judgeReached.Count,
            judgeReached.Count(r => r.Actual != r.Expected),
            results);
    }
}
