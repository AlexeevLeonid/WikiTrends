using Microsoft.Extensions.Options;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Aggregator.Workers;

public sealed class TrendUpdateWorker : BackgroundService
{
    private readonly IKafkaConsumer<string, TrendUpdateEvent> _consumer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<TrendUpdateWorker> _logger;

    public TrendUpdateWorker(
        IKafkaConsumer<string, TrendUpdateEvent> consumer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<TrendUpdateWorker> logger)
    {
        _consumer = consumer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: 1. Залогировать старт TrendUpdateWorker
        // TODO: 2. Запустить consumer для topic TrendUpdates
        // TODO: 3. Держать worker живым до остановки приложения
        _logger.LogInformation("Starting TrendUpdate Kafka consumer worker.");

        var topic = _topicsOptions.TrendUpdates;
        await _consumer.StartAsync(topic, stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // TODO: 1. Залогировать остановку TrendUpdateWorker
        // TODO: 2. Остановить consumer через StopAsync
        // TODO: 3. Вызвать base.StopAsync
        _logger.LogInformation("Stopping TrendUpdate Kafka consumer worker.");

        await _consumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
