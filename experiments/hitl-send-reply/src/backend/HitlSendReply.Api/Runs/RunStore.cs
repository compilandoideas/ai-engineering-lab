using System.Collections.Concurrent;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Contracts;
using HitlSendReply.SendReply;

namespace HitlSendReply.Api.Runs;

/// <summary>One run: its own isolated pipeline (outbox, ledger, audit) and the paused agent run.</summary>
public sealed class RunEntry(string id, RunMode mode, int? scenario, string customerMessage, Pipeline pipeline, ApprovalRun run)
{
    public string Id { get; } = id;
    public RunMode Mode { get; } = mode;
    public int? Scenario { get; } = scenario;
    public string CustomerMessage { get; } = customerMessage;
    public Pipeline Pipeline { get; } = pipeline;
    public ApprovalRun Run { get; } = run;

    /// <summary>A run is resumed by one request at a time.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>In memory: runs live as long as the process. A persistent store would serialize the MAF session.</summary>
public sealed class RunStore
{
    private readonly ConcurrentDictionary<string, RunEntry> _runs = new();

    public void Add(RunEntry entry) => _runs[entry.Id] = entry;

    public RunEntry? Find(string id) => _runs.GetValueOrDefault(id);
}
