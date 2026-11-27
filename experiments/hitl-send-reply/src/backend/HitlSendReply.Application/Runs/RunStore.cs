using System.Collections.Concurrent;

namespace HitlSendReply.Application.Runs;

/// <summary>
/// One run: its own isolated session (agent, outbox, audit) and the paused agent run. A run
/// started from an inbound email is registered first and starts in the background, so Run is
/// null while it is starting, and Error is set if starting failed.
/// </summary>
public sealed class RunEntry(string id, string ticketId, string customerMessage, RunSession session, IReplyRun? run = null)
{
    private volatile IReplyRun? _run = run;
    private volatile string? _error;

    public string Id { get; } = id;
    public string TicketId { get; } = ticketId;
    public string CustomerMessage { get; } = customerMessage;
    public RunSession Session { get; } = session;
    public IReplyRun? Run => _run;
    public string? Error => _error;

    /// <summary>Completes when a background start has finished, whatever its outcome.</summary>
    public Task Started { get; private set; } = Task.CompletedTask;

    /// <summary>A run is started or resumed by one caller at a time.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public void StartInBackground(Func<Task<IReplyRun>> start, Action<Exception> onFailure) =>
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
