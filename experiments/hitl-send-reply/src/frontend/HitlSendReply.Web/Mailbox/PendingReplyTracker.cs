namespace HitlSendReply.Web.Mailbox;

/// <summary>
/// Every two seconds, reads back the runs still in progress. Without it a reply waiting for a
/// person would only reach email_replies while someone has the inbox open.
/// </summary>
public sealed partial class PendingReplyTracker(IServiceScopeFactory scopes, ILogger<PendingReplyTracker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MailboxService>().RefreshInProgressAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // The next tick tries again; the database or the API may just be starting.
                LogTrackFailed(ex.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pending replies not refreshed: {Error}")]
    private partial void LogTrackFailed(string error);
}
