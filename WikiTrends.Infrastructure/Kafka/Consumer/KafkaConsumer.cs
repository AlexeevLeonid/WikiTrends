using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using WikiTrends.Infrastructure.Kafka.Serialization;
using WikiTrends.Infrastructure.Kafka.Settings;

namespace WikiTrends.Infrastructure.Kafka.Consumer;

public sealed class KafkaConsumer<TKey, TValue> : IKafkaConsumer<TKey, TValue>, IDisposable
{
    private readonly IConsumer<TKey, TValue> _consumer;
    private readonly IMessageHandler<TValue> _handler;
    private readonly ILogger<KafkaConsumer<TKey, TValue>> _logger;
    private readonly KafkaSettings _settings;

    private CancellationTokenSource? _stoppingCts;
    private Task? _consumeTask;
    private bool _disposed;

    public KafkaConsumer(
        IOptions<KafkaSettings> settings,
        IMessageHandler<TValue> handler,
        ILogger<KafkaConsumer<TKey, TValue>> logger)
    {
        _settings = settings.Value;
        _handler = handler;
        _logger = logger;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            // Добавил ignoreCase: true для надежности
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.AutoOffsetReset, true),
            EnableAutoCommit = _settings.EnableAutoCommit,
            SessionTimeoutMs = _settings.SessionTimeoutMs,
            MaxPollIntervalMs = _settings.MaxPollIntervalMs,
        };

        var consumerBuilder = new ConsumerBuilder<TKey, TValue>(consumerConfig)
            .SetValueDeserializer(new KafkaJsonDeserializer<TValue>());

        if (typeof(TKey) != typeof(string))
        {
            consumerBuilder.SetKeyDeserializer(new KafkaJsonDeserializer<TKey>());
        }

        consumerBuilder.SetErrorHandler((_, e) =>
            _logger.LogError("Error in Kafka consumer: Code: {Code}, Reason: {Reason}", e.Code, e.Reason));

        _consumer = consumerBuilder.Build();
    }

    public Task StartAsync(string topic, CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _consumer.Subscribe(topic);
        _logger.LogInformation("Starting Kafka consumer for topic {Topic}", topic);

        _consumeTask = Task.Run(() => ConsumeLoopAsync(_stoppingCts.Token));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_consumeTask == null) return;

        _logger.LogInformation("Stopping Kafka consumer...");

        _stoppingCts?.Cancel();

        var cleanupTask = Task.WhenAny(_consumeTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
        await cleanupTask;

        try
        {
            _consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing consumer");
        }

        _logger.LogInformation("Kafka consumer stopped");
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var metricsEnabled = _settings.MetricsEnabled;
        var metricsInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.MetricsLogIntervalSeconds));
        var slowThresholdMs = Math.Max(0, _settings.SlowMessageThresholdMs);

        var intervalStopwatch = Stopwatch.StartNew();
        long intervalProcessed = 0;
        long intervalTotalProcessingMs = 0;
        long intervalSlowCount = 0;
        long intervalMaxProcessingMs = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));

                    if (result == null || result.IsPartitionEOF)
                        continue;

                    _logger.LogDebug("Received message from {Topic}[{Partition}]@{Offset}",
                        result.Topic, result.Partition.Value, result.Offset.Value);

                    var processingSw = Stopwatch.StartNew();
                    await _handler.HandleAsync(result.Message.Value, stoppingToken);
                    processingSw.Stop();

                    if (metricsEnabled)
                    {
                        intervalProcessed++;
                        intervalTotalProcessingMs += processingSw.ElapsedMilliseconds;
                        intervalMaxProcessingMs = Math.Max(intervalMaxProcessingMs, processingSw.ElapsedMilliseconds);

                        if (slowThresholdMs > 0 && processingSw.ElapsedMilliseconds >= slowThresholdMs)
                        {
                            intervalSlowCount++;
                            _logger.LogWarning(
                                "Metric={Metric} Topic={Topic} Partition={Partition} Offset={Offset} ProcessingMs={ProcessingMs} ThresholdMs={ThresholdMs}",
                                "kafka_message_slow",
                                result.Topic,
                                result.Partition.Value,
                                result.Offset.Value,
                                processingSw.ElapsedMilliseconds,
                                slowThresholdMs);
                        }

                        if (intervalStopwatch.Elapsed >= metricsInterval)
                        {
                            var seconds = Math.Max(0.001, intervalStopwatch.Elapsed.TotalSeconds);
                            var eps = intervalProcessed / seconds;
                            var avgMs = intervalProcessed > 0
                                ? intervalTotalProcessingMs / (double)intervalProcessed
                                : 0;

                            _logger.LogInformation(
                                "Metric={Metric} Topic={Topic} GroupId={GroupId} IntervalSec={IntervalSec} Processed={Processed} EventsPerSec={EventsPerSec} AvgProcessingMs={AvgProcessingMs} MaxProcessingMs={MaxProcessingMs} SlowCount={SlowCount}",
                                "kafka_consumer_stats",
                                result.Topic,
                                _settings.GroupId,
                                (int)intervalStopwatch.Elapsed.TotalSeconds,
                                intervalProcessed,
                                eps,
                                avgMs,
                                intervalMaxProcessingMs,
                                intervalSlowCount);

                            intervalProcessed = 0;
                            intervalTotalProcessingMs = 0;
                            intervalSlowCount = 0;
                            intervalMaxProcessingMs = 0;
                            intervalStopwatch.Restart();
                        }
                    }

                    if (!_settings.EnableAutoCommit)
                    {
                        _consumer.Commit(result);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Consume exception: {Reason}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error in consume loop");

                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stoppingCts?.Cancel();
        _stoppingCts?.Dispose();

        _consumer.Dispose();

        _logger.LogInformation("Kafka consumer disposed");
    }
}