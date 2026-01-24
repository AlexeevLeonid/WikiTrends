using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WikiTrends.Collector.Mapping;
using WikiTrends.Collector.Services;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka;
using WikiTrends.Infrastructure.Kafka.Producer;

namespace WikiTrends.Collector.Workers;

/// <summary>
/// Background worker для сбора событий из Wikipedia EventStreams.
/// </summary>
public sealed class WikiStreamWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaProducer<string, RawEditEvent> _producer;
    private readonly TopicsOptions _topicsOptions;
    private readonly WikiStreamOptions _options;
    private readonly ILogger<WikiStreamWorker> _logger;

    private long _processedCount;
    private long _publishedCount;
    private long _errorCount;
    private DateTimeOffset _lastEventTime = DateTimeOffset.MinValue;

    public WikiStreamWorker(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer<string, RawEditEvent> producer,
        IOptions<TopicsOptions> topicsOptions,
        IOptions<WikiStreamOptions> options,
        ILogger<WikiStreamWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _topicsOptions = topicsOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");
        await Task.Delay(1000);
        var reconnectDelaySeconds = _options.ReconnectDelaySeconds;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStreamAsync(stoppingToken);
                reconnectDelaySeconds = _options.ReconnectDelaySeconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker error occurred");
                _errorCount++;
                await Task.Delay(reconnectDelaySeconds * 1000);
                reconnectDelaySeconds = int.Min(reconnectDelaySeconds * 2, _options.MaxReconnectDelaySeconds);
            }
        }

        _logger.LogInformation("Worker stopped");
    }

    private async Task ProcessStreamAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var streamClient = scope.ServiceProvider.GetRequiredService<IWikiStreamClient>() ?? throw new Exception("Stream Client is null");
        var mapper = scope.ServiceProvider.GetRequiredService<IEditMapper>();
        await streamClient.ConnectAsync(null, ct);
        _logger.LogInformation("Connection success. Last event id: [{0}]", streamClient.LastEventId);
        while (!ct.IsCancellationRequested && streamClient.IsConnected)
        {
            var change = await streamClient.ReadEventAsync(ct);
            if (change == null) break;
            _processedCount++;
            _lastEventTime = DateTimeOffset.UtcNow;
            var mappedEvent = mapper.Map(change);
            if (mappedEvent == null) continue;
            await PublishEventAsync(mappedEvent, ct);
            if (_processedCount % Constants.Intervals.LogStatisticsEveryNEvents == 0)
                LogStatistics();
        }
        await streamClient.DisposeAsync();
        _logger.LogInformation("Connection closed.");
    }

    private async Task PublishEventAsync(RawEditEvent editEvent, CancellationToken ct)
    {
        var key = editEvent.PageId.ToString();
        var topic = _topicsOptions.RawEdits;
        try
        {
            await _producer.ProduceAsync(topic, key, editEvent, ct);
            _logger.LogDebug("Publish success. Title: [{0}], Wiki: [{1}], WikiEditId: [{2}]", 
                editEvent.Title, editEvent.Wiki, editEvent.WikiEditId);
            _publishedCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publish error occurred");
            _errorCount++;
        }
    }

    private void LogStatistics()
    {
        _logger.LogInformation("Processedcount : [{0}] PublishedCount : [{1}] ErrorCount : [{2}] lastEventTime : [{3}]",
            _processedCount, _publishedCount, _errorCount, _lastEventTime.ToString());
    }
}