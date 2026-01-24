using Microsoft.Extensions.Options;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Services;

namespace WikiTrends.Analytics.Workers;

public sealed class TrendCalculationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<TrendCalculationWorker> _logger;

    public TrendCalculationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AnalyticsOptions> options,
        ILogger<TrendCalculationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.TrendCalculationIntervalSeconds));
        _logger.LogInformation("Starting trend calculation worker. Interval: {Interval}", interval);

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ITrendCalculationService>();
                await service.CalculateAndPublishAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trend calculation cycle failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
