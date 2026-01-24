using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Services;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Enricher.Handlers;

public sealed class RawEditHandler : IMessageHandler<RawEditEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RawEditHandler> _logger;

    public RawEditHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<RawEditHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(RawEditEvent message, CancellationToken cancellationToken)
    {
        //  1. Залогировать получение RawEditEvent (Wiki, Title, WikiEditId)
        //  2. Создать scope через _scopeFactory.CreateScope()
        //  3. Получить IEnrichmentService из scope.ServiceProvider
        //  4. Вызвать enrichmentService.EnrichAsync(message, cancellationToken)
        //  3. Если результат неуспешный — залогировать warning и выйти (не бросать исключение наружу)
        //  4. Если успех — залогировать итог (например, ArticleId, кол-во категорий)
        //  5. Обернуть всю обработку в try/catch, в catch логировать ошибку и продолжать (не пробрасывать)
        _logger.LogInformation("Received RawEditEvent ({Wiki}, {Title}, {WikiEditId})", 
            message.Wiki, message.Title, message.WikiEditId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var enrichmentService = scope.ServiceProvider.GetRequiredService<IEnrichmentService>();
            var enrResult = await enrichmentService.EnrichAsync(message, cancellationToken);
            if (!enrResult.IsSuccess)
            {
                _logger.LogWarning("Enrichment error on ({Wiki}, {Title}, {WikiEditId}): {Error}",
                message.Wiki, message.Title, message.WikiEditId, enrResult.Error);
                return;
            }
            else
            {
                _logger.LogInformation("Enrichment success on ({Wiki}, {Title}, {WikiEditId}): Categories: {Count}",
                    message.Wiki, message.Title, message.WikiEditId, enrResult.Value!.Categories.Count);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing ({Wiki}, {Title}, {WikiEditId})",
                message.Wiki, message.Title, message.WikiEditId);
            return;
        }
    }
}
