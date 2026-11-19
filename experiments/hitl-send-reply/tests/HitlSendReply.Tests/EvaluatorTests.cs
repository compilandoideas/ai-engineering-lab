using HitlSendReply.Evaluation;
using HitlSendReply.Judge;
using HitlSendReply.Policy;

namespace HitlSendReply.Tests;

public class EvaluatorTests
{
    private static readonly IReadOnlyList<LabeledDraft> Drafts =
        Evaluator.Load(Path.Combine(AppContext.BaseDirectory, "data", "drafts.jsonl"));

    [Fact]
    public void Dataset_has_thirty_labeled_drafts() => Assert.Equal(30, Drafts.Count);

    [Fact]
    public async Task With_a_judge_that_never_escalates_only_judge_dependent_drafts_leak()
    {
        // Shows what the deterministic layer catches on its own, and what is left to judgment.
        var report = await Evaluator.RunOnceAsync(1, Drafts, ScriptedJudge.AlwaysClear(), TimeSpan.FromSeconds(5));

        var judgeDependent = Drafts.Count(d => d.Note.StartsWith("judge:", StringComparison.Ordinal));
        Assert.Equal(judgeDependent, report.FalseNegatives);
        Assert.Equal(0, report.FalsePositives);
        Assert.All(report.Items.Where(i => !i.Note.StartsWith("judge:", StringComparison.Ordinal)),
            i => Assert.Equal(i.Expected, i.Actual));
    }

    [Fact]
    public async Task With_a_judge_that_always_escalates_nothing_is_auto_sent()
    {
        var report = await Evaluator.RunOnceAsync(1, Drafts,
            ScriptedJudge.AlwaysEscalate(JudgeCategory.Other, "unsure"), TimeSpan.FromSeconds(5));

        Assert.Equal(0, report.FalseNegatives);
        Assert.Equal(Drafts.Count(d => d.Expected == PolicyOutcome.AutoSend), report.FalsePositives);
    }
}
