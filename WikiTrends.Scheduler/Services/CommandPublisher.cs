using WikiTrends.Contracts.Events;
using Microsoft.Extensions.Options;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;

namespace WikiTrends.Scheduler.Services;

public sealed class CommandPublisher : ICommandPublisher
{
    private readonly IKafkaProducer<string, RecalculateBaselineCommand> _recalculateProducer;
    private readonly IKafkaProducer<string, InvalidateCacheCommand> _invalidateCacheProducer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<CommandPublisher> _logger;

    public CommandPublisher(
        IKafkaProducer<string, RecalculateBaselineCommand> recalculateProducer,
        IKafkaProducer<string, InvalidateCacheCommand> invalidateCacheProducer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<CommandPublisher> logger)
    {
        _recalculateProducer = recalculateProducer;
        _invalidateCacheProducer = invalidateCacheProducer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    public async Task PublishRecalculateBaselineAsync(RecalculateBaselineCommand command, CancellationToken ct = default)
    {
        // TODO: 1. Определить топик команды пересчёта baseline
        // TODO: 2. Определить ключ партиционирования (например, topicId или "all")
        // TODO: 3. Вызвать _recalculateProducer.ProduceAsync(topic, key, command, ct)
        // TODO: 4. Логировать отправку
        var topic = _topicsOptions.RecalculateBaselineCommands;
        var key = command.TopicId?.ToString() ?? "all";

        var delivery = await _recalculateProducer.ProduceAsync(topic, key, command, ct);

        _logger.LogInformation(
            "Published {CommandType} to {Topic}[{Partition}]@{Offset}. TopicId={TopicId}. RequestedAt={RequestedAt}",
            nameof(RecalculateBaselineCommand),
            delivery.Topic,
            delivery.Partition.Value,
            delivery.Offset.Value,
            command.TopicId,
            command.RequestedAt);
    }

    public async Task PublishInvalidateCacheAsync(InvalidateCacheCommand command, CancellationToken ct = default)
    {
        // TODO: 1. Определить топик команды инвалидации кэша
        // TODO: 2. Определить ключ партиционирования (например, command.CacheKey)
        // TODO: 3. Вызвать _invalidateCacheProducer.ProduceAsync(topic, key, command, ct)
        // TODO: 4. Логировать отправку
        var topic = _topicsOptions.InvalidateCacheCommands;
        var key = command.CacheKey;

        var delivery = await _invalidateCacheProducer.ProduceAsync(topic, key, command, ct);

        _logger.LogInformation(
            "Published {CommandType} to {Topic}[{Partition}]@{Offset}. CacheKey={CacheKey}. RequestedAt={RequestedAt}",
            nameof(InvalidateCacheCommand),
            delivery.Topic,
            delivery.Partition.Value,
            delivery.Offset.Value,
            command.CacheKey,
            command.RequestedAt);
    }
}
