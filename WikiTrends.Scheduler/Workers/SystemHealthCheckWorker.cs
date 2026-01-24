using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WikiTrends.Scheduler.Configuration;
using WikiTrends.Scheduler.Jobs;

namespace WikiTrends.Scheduler.Workers;

public sealed class SystemHealthCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SchedulerOptions> _options;
    private readonly ILogger<SystemHealthCheckWorker> _logger;

    public SystemHealthCheckWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerOptions> options,
        ILogger<SystemHealthCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.Value.SystemHealthCheckIntervalMinutes);

        _logger.LogInformation("SystemHealthCheckWorker started. IntervalMinutes={IntervalMinutes}",
            _options.Value.SystemHealthCheckIntervalMinutes);

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }

        _logger.LogInformation("SystemHealthCheckWorker stopped.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<SystemHealthCheckJob>();

            await job.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemHealthCheckWorker iteration failed.");
        }
    }
}
