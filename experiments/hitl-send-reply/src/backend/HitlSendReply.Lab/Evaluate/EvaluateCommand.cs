using System.Text.Json;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Evaluate;

/// <summary><c>evaluate</c>: measures the real judge on the labeled drafts and writes the results to <c>results/</c>.</summary>
public static class EvaluateCommand
{
    public static async Task<int> RunAsync(int runs, Language language)
    {
        if (LlmSettings.FromEnvironment() is not { } settings)
        {
            Console.Error.WriteLine(LlmSettings.MissingMessage);
            return 2;
        }

        using var judgeModel = settings.CreateChatClient(settings.JudgeModel);
        var judge = settings.CreateJudge(judgeModel);
        var drafts = Evaluator.Load(Evaluator.DatasetPath(language));
        var reports = new List<RunReport>();

        for (var run = 1; run <= runs; run++)
        {
            var report = await Evaluator.RunOnceAsync(run, drafts, judge, TimeSpan.FromSeconds(60), language);
            reports.Add(report);
            Console.WriteLine($"Run {run}: correct {report.Correct}/{report.Total} · false negatives {report.FalseNegatives} · " +
                              $"false positives {report.FalsePositives} · judge misses {report.JudgeMisses}/{report.JudgeReached}");
            foreach (var miss in report.Items.Where(i => i.Actual != i.Expected))
            {
                Console.WriteLine($"   {miss.Id} expected {miss.Expected}, got {miss.Actual} at {miss.Stage}: {miss.Note}");
            }
        }

        var evaluation = new EvaluationReport(settings.JudgeModel, language.ToString().ToLowerInvariant(), DateTimeOffset.UtcNow, reports);
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "results");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{evaluation.At:yyyyMMdd-HHmmss}-{evaluation.Language}-{string.Join("_", settings.JudgeModel.Split(Path.GetInvalidFileNameChars()))}.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(evaluation, Evaluator.Json));
        Console.WriteLine($"Results written to {file}");
        return 0;
    }
}
