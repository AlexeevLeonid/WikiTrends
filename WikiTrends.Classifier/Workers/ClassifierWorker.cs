using WikiTrends.Contracts.Events;
using Microsoft.Extensions.Options;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;
using System.Threading;

namespace WikiTrends.Classifier.Workers;

public sealed class ClassifierWorker : BackgroundService
{
    private static int _instanceCounter;

    private readonly int _workerId;
    private readonly IKafkaConsumer<string, EnrichedEditEvent> _consumer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<ClassifierWorker> _logger;

    public ClassifierWorker(
        IKafkaConsumer<string, EnrichedEditEvent> consumer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<ClassifierWorker> logger)
    {
        _workerId = Interlocked.Increment(ref _instanceCounter);
        _consumer = consumer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started. WorkerId={WorkerId}", _workerId);
        var topic = _topicsOptions.EnrichedEdits;
        await _consumer.StartAsync(topic, stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopped. WorkerId={WorkerId}", _workerId);
        await _consumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
