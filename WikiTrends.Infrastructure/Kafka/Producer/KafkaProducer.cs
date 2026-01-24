using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WikiTrends.Infrastructure.Kafka.Serialization;
using WikiTrends.Infrastructure.Kafka.Settings;

namespace WikiTrends.Infrastructure.Kafka.Producer;

/// <summary>
/// Реализация Kafka Producer с JSON сериализацией.
/// Thread-safe, можно использовать как singleton.
/// </summary>
// Добавляем IDisposable явно, если он не наследуется от IKafkaProducer
public sealed class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>, IDisposable
{
    private readonly IProducer<TKey, TValue> _producer;
    private readonly ILogger<KafkaProducer<TKey, TValue>> _logger;
    private bool _disposed;

    public KafkaProducer(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaProducer<TKey, TValue>> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,             // Гарантия доставки
            EnableIdempotence = true,    // Гарантия порядка и отсутствия дублей
            // LingerMs = 5              // Можно добавить небольшую задержку для батчинга (опционально)
        };

        var producerBuilder = new ProducerBuilder<TKey, TValue>(producerConfig)
            .SetValueSerializer(new KafkaJsonSerializer<TValue>());

        if (typeof(TKey) != typeof(string))
        {
            producerBuilder.SetKeySerializer(new KafkaJsonSerializer<TKey>());
        }

        _producer = producerBuilder.Build();

        _logger.LogInformation("Kafka producer created for {TypeName}", typeof(TValue).Name);
    }

    public Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        return ProduceAsync(topic, key, value, null, cancellationToken);
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        Headers? headers,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var message = new Message<TKey, TValue>
        {
            Key = key,
            Value = value,
            Headers = headers
        };

        try
        {
            var res = await _producer.ProduceAsync(topic, message, cancellationToken);

            _logger.LogInformation("Message delivered to {Topic}[{Partition}]@{Offset}",
                res.Topic, res.Partition.Value, res.Offset.Value);

            return res;
        }
        catch (ProduceException<TKey, TValue> ex)
        {
            _logger.LogError(ex, "Failed to deliver message to {Topic}. Reason: {Reason}", topic, ex.Error.Reason);
            throw;
        }
    }

    public void Flush(TimeSpan timeout)
    {
        ThrowIfDisposed();
        _producer.Flush(timeout);
        _logger.LogInformation("Producer flushed");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during producer flush on dispose");
        }

        _producer.Dispose();
        _logger.LogInformation("Kafka producer disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaProducer<TKey, TValue>), "Producer has been disposed");
    }
}