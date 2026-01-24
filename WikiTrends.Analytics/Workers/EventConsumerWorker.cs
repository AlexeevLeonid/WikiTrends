using WikiTrends.Contracts.Events;
using Microsoft.Extensions.Options;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Analytics.Workers;

public sealed class EventConsumerWorker : BackgroundService
{
    private readonly IKafkaConsumer<string, ClassifiedEditEvent> _consumer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<EventConsumerWorker> _logger;

    public EventConsumerWorker(
        IKafkaConsumer<string, ClassifiedEditEvent> consumer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<EventConsumerWorker> logger)
    {
        _consumer = consumer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting analytics Kafka consumer worker.");

        var topic = _topicsOptions.ClassifiedEdits;
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
        _logger.LogInformation("Stopping analytics Kafka consumer worker.");

        await _consumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
