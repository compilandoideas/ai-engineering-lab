using HitlSendReply.Domain;
using HitlSendReply.Evaluation;
using HitlSendReply.Judge;
using HitlSendReply.Policy;

namespace HitlSendReply.Tests;

public class EvaluatorTests
{
    private static Language L(string lang) => lang == "es" ? Language.Es : Language.En;

    private static IReadOnlyList<LabeledDraft> Drafts(string lang) => Evaluator.Load(Evaluator.DatasetPath(L(lang)));

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void Dataset_has_a_hundred_labeled_drafts_with_the_same_label_mix(string lang)
    {
        var drafts = Drafts(lang);
        Assert.Equal(100, drafts.Count);
        Assert.Equal(30, drafts.Count(d => d.Expected == PolicyOutcome.AutoSend));
        Assert.Equal(67, drafts.Count(d => d.Expected == PolicyOutcome.NeedsHuman));
        Assert.Equal(3, drafts.Count(d => d.Expected == PolicyOutcome.Block));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task With_a_judge_that_never_escalates_only_judge_dependent_drafts_leak(string lang)
    {
        // Shows what the deterministic layer catches on its own, and what is left to judgment.
        var drafts = Drafts(lang);
        var report = await Evaluator.RunOnceAsync(1, drafts, ScriptedJudge.AlwaysClear(), TimeSpan.FromSeconds(5), L(lang));

        var judgeDependent = drafts.Count(d => d.Note.StartsWith("judge:", StringComparison.Ordinal));
        Assert.Equal(60, judgeDependent);
        Assert.Equal(judgeDependent, report.FalseNegatives);
        Assert.Equal(0, report.FalsePositives);
        Assert.All(report.Items.Where(i => !i.Note.StartsWith("judge:", StringComparison.Ordinal)),
            i => Assert.Equal(i.Expected, i.Actual));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task With_a_judge_that_always_escalates_nothing_is_auto_sent(string lang)
    {
        var drafts = Drafts(lang);
        var report = await Evaluator.RunOnceAsync(1, drafts,
            ScriptedJudge.AlwaysEscalate(JudgeCategory.Other, "unsure"), TimeSpan.FromSeconds(5), L(lang));

        Assert.Equal(0, report.FalseNegatives);
        Assert.Equal(30, report.FalsePositives);
    }
}
