using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using HitlSendReply.Domain;
using HitlSendReply.Evaluation;
using HitlSendReply.Judge;
using HitlSendReply.Policy;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

// harness2 <es|en> <model> <simple|rubric> <runs> <out.jsonl>
var lang = args[0] == "es" ? Language.Es : Language.En;
var drafts = Evaluator.Load(args.Length > 5 ? args[5] : Evaluator.DatasetPath(lang));
var clear = await Evaluator.RunOnceAsync(1, drafts, ScriptedJudge.AlwaysClear(), TimeSpan.FromSeconds(5), lang);
var reach = clear.Items.Where(i => i.Stage == PolicyStage.Judge).Select(i => i.Id).ToHashSet();
File.WriteAllText(args[4] + ".pre.json", JsonSerializer.Serialize(clear.Items.ToDictionary(i => i.Id, i => new { expected = i.Expected.ToString(), stage = i.Stage.ToString(), actual = i.Actual.ToString(), reason = i.Reason, note = i.Note, text = drafts.First(d => d.Id == i.Id).Text })));
var model = args[1]; var kind = args[2]; var runs = int.Parse(args[3]);
var inner = new OpenAIClient(new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)).GetChatClient(model).AsIChatClient();
var tickets = SampleTickets.Store(lang);
var work = (from d in drafts where reach.Contains(d.Id) from r in Enumerable.Range(1, runs) select (d, r)).ToList();
var results = new ConcurrentBag<string>();
await Parallel.ForEachAsync(work, new ParallelOptions { MaxDegreeOfParallelism = 12 }, async (w, ct) =>
{
    for (var attempt = 0; ; attempt++)
    {
        var meter = new Meter(inner);
        IReplyJudge judge = kind == "rubric" ? new RubricReplyJudge(meter) : new LlmReplyJudge(meter);
        var sw = Stopwatch.StartNew();
        try
        {
            var s = await judge.EvaluateAsync(w.d.Text, tickets.Find(w.d.TicketId)!, ct);
            results.Add(JsonSerializer.Serialize(new { id = w.d.Id, run = w.r, expected = w.d.Expected.ToString(), note = w.d.Note,
                escalate = s.Escalate, category = s.Category.ToString(), rationale = s.Rationale,
                ms = sw.ElapsedMilliseconds, inTok = meter.In, outTok = meter.Out }));
            break;
        }
        catch (Exception) when (attempt < 4) { await Task.Delay(2000 * (attempt + 1), ct); }
    }
});
File.WriteAllLines(args[4], results.OrderBy(x => x));
Console.WriteLine($"{lang} {model} {kind}: {work.Count} -> {args[4]}");

sealed class Meter(IChatClient inner) : DelegatingChatClient(inner)
{
    public long In, Out;
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var r = await base.GetResponseAsync(messages, options, cancellationToken);
        In += r.Usage?.InputTokenCount ?? 0; Out += r.Usage?.OutputTokenCount ?? 0;
        return r;
    }
}
