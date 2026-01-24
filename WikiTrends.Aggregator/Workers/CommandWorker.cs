using WikiTrends.Contracts.Events;
using Microsoft.Extensions.Options;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Aggregator.Workers;

public sealed class CommandWorker : BackgroundService
{
    private readonly IKafkaConsumer<string, RecalculateBaselineCommand> _recalculateBaselineConsumer;
    private readonly IKafkaConsumer<string, InvalidateCacheCommand> _invalidateCacheConsumer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<CommandWorker> _logger;

    public CommandWorker(
        IKafkaConsumer<string, RecalculateBaselineCommand> recalculateBaselineConsumer,
        IKafkaConsumer<string, InvalidateCacheCommand> invalidateCacheConsumer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<CommandWorker> logger)
    {
        _recalculateBaselineConsumer = recalculateBaselineConsumer;
        _invalidateCacheConsumer = invalidateCacheConsumer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: 1. Залогировать старт CommandWorker
        // TODO: 2. Определить топики команд (например, "wiki.commands.recalculate-baseline" и "wiki.commands.invalidate-cache")
        // TODO: 3. Запустить оба consumer'а через StartAsync
        // TODO: 4. Держать worker живым до остановки приложения
        _logger.LogInformation("Starting CommandWorker.");

        await _recalculateBaselineConsumer.StartAsync(_topicsOptions.RecalculateBaselineCommands, stoppingToken);
        await _invalidateCacheConsumer.StartAsync(_topicsOptions.InvalidateCacheCommands, stoppingToken);

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
        // TODO: 1. Залогировать остановку CommandWorker
        // TODO: 2. Остановить оба consumer'а через StopAsync
        // TODO: 3. Вызвать base.StopAsync
        _logger.LogInformation("Stopping CommandWorker.");

        await _recalculateBaselineConsumer.StopAsync(cancellationToken);
        await _invalidateCacheConsumer.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
