using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Kafka.Consumer;
using WikiTrends.Infrastructure.Configuration;
using System.Threading;

namespace WikiTrends.Enricher.Workers;

public sealed class EnricherWorker : BackgroundService
{
    private static int _instanceCounter;

    private readonly int _workerId;
    private readonly IKafkaConsumer<string, RawEditEvent> _consumer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<EnricherWorker> _logger;

    public EnricherWorker(
        IKafkaConsumer<string, RawEditEvent> consumer,
        Microsoft.Extensions.Options.IOptions<TopicsOptions> topicsOptions,
        ILogger<EnricherWorker> logger)
    {
        _workerId = Interlocked.Increment(ref _instanceCounter);
        _consumer = consumer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enricher worker started. WorkerId={WorkerId}", _workerId);
        var topic = _topicsOptions.RawEdits;
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
        _logger.LogInformation("Enricher worker stopped. WorkerId={WorkerId}", _workerId);
        await _consumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
