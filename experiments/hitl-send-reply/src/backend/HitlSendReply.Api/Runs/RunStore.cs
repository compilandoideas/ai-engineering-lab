using System.Collections.Concurrent;
using HitlSendReply.Api.Scenarios;
using HitlSendReply.Contracts;
using HitlSendReply.SendReply;

namespace HitlSendReply.Api.Runs;

/// <summary>
/// One run: its own isolated pipeline (outbox, ledger, audit) and the paused agent run. A run
/// started from an inbound email is registered first and starts in the background, so Run is
/// null while it is starting, and Error is set if starting failed.
/// </summary>
public sealed class RunEntry(string id, RunMode mode, int? scenario, string ticketId, string customerMessage, Pipeline pipeline, ApprovalRun? run = null)
{
    private volatile ApprovalRun? _run = run;
    private volatile string? _error;

    public string Id { get; } = id;
    public RunMode Mode { get; } = mode;
    public int? Scenario { get; } = scenario;
    public string TicketId { get; } = ticketId;
    public string CustomerMessage { get; } = customerMessage;
    public Pipeline Pipeline { get; } = pipeline;
    public ApprovalRun? Run => _run;
    public string? Error => _error;

    /// <summary>Completes when a background start has finished, whatever its outcome.</summary>
    public Task Started { get; private set; } = Task.CompletedTask;

    /// <summary>A run is started or resumed by one caller at a time.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public void StartInBackground(Func<Task<ApprovalRun>> start, Action<Exception> onFailure) =>
        Started = Task.Run(async () =>
        {
            await Gate.WaitAsync();
            try
            {
                _run = await start();
            }
            catch (Exception ex)
            {
                // Nobody awaits this task: the failure is kept on the run, where GET /api/runs shows it.
                _error = ex.Message;
                onFailure(ex);
            }
            finally
            {
                Gate.Release();
            }
        });
}

/// <summary>In memory: runs live as long as the process. A persistent store would serialize the MAF session.</summary>
public sealed class RunStore
{
    private readonly ConcurrentDictionary<string, RunEntry> _runs = new();

    public void Add(RunEntry entry) => _runs[entry.Id] = entry;

    public RunEntry? Find(string id) => _runs.GetValueOrDefault(id);
}
