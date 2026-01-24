using WikiTrends.Aggregator.Services;
using Microsoft.Extensions.Options;
using WikiTrends.Aggregator.Configuration;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Aggregator.Workers;

public sealed class TrendCacheWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AggregatorOptions _options;
    private readonly ILogger<TrendCacheWorker> _logger;

    public TrendCacheWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AggregatorOptions> options,
        ILogger<TrendCacheWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: 1. Залогировать старт TrendCacheWorker
        // TODO: 2. Определить интервал обновления кэша (из конфигурации)
        // TODO: 3. В цикле: создать scope, получить IAggregationService
        // TODO: 4. Вызвать GetTrendsAsync для нужных периодов (LastHour/Last24Hours/Last7Days)
        // TODO: 5. Логировать результат и обработать ошибки
        // TODO: 6. Delay до следующего цикла
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.TrendCacheRefreshSeconds));
        _logger.LogInformation("Starting TrendCacheWorker. Interval: {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAggregationService>();

                foreach (var period in Enum.GetValues<TrendPeriod>())
                {
                    var result = await service.GetTrendsAsync(new GetTrendsRequest
                    {
                        Period = period
                    }, stoppingToken);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Cache warm-up OK for {Period}. Topics: {Count}", period, result.Value?.Topics.Count ?? 0);
                    }
                    else
                    {
                        _logger.LogWarning("Cache warm-up failed for {Period}: {Error}", period, result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TrendCacheWorker cycle failed.");
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
