using WikiTrends.Aggregator.Services;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Aggregator.Handlers;

public sealed class TrendUpdateHandler : IMessageHandler<TrendUpdateEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrendUpdateHandler> _logger;

    public TrendUpdateHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<TrendUpdateHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(TrendUpdateEvent message, CancellationToken cancellationToken)
    {
        // TODO: 1. Залогировать получение TrendUpdateEvent (Period, CalculatedAt)
        // TODO: 2. Создать scope через _scopeFactory.CreateScope()
        // TODO: 3. Получить IAggregationService из scope.ServiceProvider
        // TODO: 4. Вызвать aggregationService.HandleTrendUpdateAsync(message, cancellationToken)
        // TODO: 5. В catch: логировать и продолжать (не пробрасывать)
        if (message == null)
        {
            _logger.LogWarning("Received null TrendUpdateEvent.");
            return;
        }

        using var logScope = _logger.BeginScope(new { message.Period, message.CalculatedAt, message.EventId });
        _logger.LogInformation("Received TrendUpdateEvent.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var aggregationService = scope.ServiceProvider.GetRequiredService<IAggregationService>();
            await aggregationService.HandleTrendUpdateAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle TrendUpdateEvent.");
        }
    }
}
