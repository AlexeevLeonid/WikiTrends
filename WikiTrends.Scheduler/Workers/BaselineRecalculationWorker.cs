using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WikiTrends.Scheduler.Configuration;
using WikiTrends.Scheduler.Jobs;

namespace WikiTrends.Scheduler.Workers;

public sealed class BaselineRecalculationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SchedulerOptions> _options;
    private readonly ILogger<BaselineRecalculationWorker> _logger;

    public BaselineRecalculationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerOptions> options,
        ILogger<BaselineRecalculationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.Value.BaselineRecalculationIntervalMinutes);

        _logger.LogInformation("BaselineRecalculationWorker started. IntervalMinutes={IntervalMinutes}",
            _options.Value.BaselineRecalculationIntervalMinutes);

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }

        _logger.LogInformation("BaselineRecalculationWorker stopped.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<BaselineRecalculationJob>();

            await job.ExecuteAsync(topicId: null, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaselineRecalculationWorker iteration failed.");
        }
    }
}
