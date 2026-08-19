namespace Workforce.Api.Services;

public sealed class AuditRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionWorker> _logger;

    public AuditRetentionWorker(IServiceScopeFactory scopeFactory, ILogger<AuditRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunAsync(stoppingToken);
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<GdprService>().CleanupAuditEntriesAsync(90);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Audit retention cleanup failed"); }
    }
}
