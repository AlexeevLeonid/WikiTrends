using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.Services;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Aggregator.Handlers;

public sealed class CommandHandler :
    IMessageHandler<RecalculateBaselineCommand>,
    IMessageHandler<InvalidateCacheCommand>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<CommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(RecalculateBaselineCommand message, CancellationToken cancellationToken)
    {
        // TODO: 1. Залогировать получение RecalculateBaselineCommand
        // TODO: 2. Создать scope и получить необходимые сервисы (например, проксировать в Analytics)
        // TODO: 3. Запустить пересчёт baseline (реализация будет позже)
        // TODO: 4. Обработать ошибки без проброса наружу
        if (message == null)
        {
            _logger.LogWarning("Received null RecalculateBaselineCommand.");
            return;
        }

        using var scope = _logger.BeginScope(new { message.TopicId, message.RequestedAt });
        _logger.LogInformation("Received RecalculateBaselineCommand.");

        try
        {
            using var diScope = _scopeFactory.CreateScope();
            var cache = diScope.ServiceProvider.GetRequiredService<ICacheService>();

            foreach (var period in Enum.GetValues<TrendPeriod>())
            {
                await cache.RemoveAsync(CacheKeys.GetTrendsKey(period), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle RecalculateBaselineCommand.");
        }
    }

    public async Task HandleAsync(InvalidateCacheCommand message, CancellationToken cancellationToken)
    {
        // TODO: 1. Залогировать получение InvalidateCacheCommand (CacheKey)
        // TODO: 2. Создать scope и получить ICacheService
        // TODO: 3. Вызвать cache.RemoveAsync(message.CacheKey, cancellationToken)
        // TODO: 4. Обработать ошибки без проброса наружу
        if (message == null)
        {
            _logger.LogWarning("Received null InvalidateCacheCommand.");
            return;
        }

        using var logScope = _logger.BeginScope(new { message.CacheKey, message.RequestedAt });
        _logger.LogInformation("Received InvalidateCacheCommand.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await cache.RemoveAsync(message.CacheKey, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle InvalidateCacheCommand.");
        }
    }
}
