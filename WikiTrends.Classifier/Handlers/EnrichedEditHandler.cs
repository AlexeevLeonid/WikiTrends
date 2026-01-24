using WikiTrends.Contracts.Events;
using WikiTrends.Classifier.Services;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Classifier.Handlers;

public sealed class EnrichedEditHandler : IMessageHandler<EnrichedEditEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrichedEditHandler> _logger;

    public EnrichedEditHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<EnrichedEditHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(EnrichedEditEvent message, CancellationToken cancellationToken)
    {
        //  1. Залогировать получение EnrichedEditEvent (Wiki, Title, ArticleId, WikiEditId)
        //  2. Создать scope через _scopeFactory.CreateScope()
        //  3. Получить IClassificationService из scope.ServiceProvider
        //  4. Вызвать classificationService.ClassifyAsync(message, cancellationToken)
        //  5. Если результат неуспешный — залогировать warning и выйти (не бросать исключение наружу)
        //  6. Если успех — залогировать итог (например, кол-во topics)
        //  7. Обернуть обработку в try/catch, в catch логировать ошибку и продолжать (не пробрасывать)
        _logger.LogInformation("Received EnrichedEditEvent ({Wiki}, {Title}, {WikiEditId})",
            message.Wiki, message.Title, message.WikiEditId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var enrichmentService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
            var enrResult = await enrichmentService.ClassifyAsync(message, cancellationToken);
            if (!enrResult.IsSuccess)
            {
                _logger.LogWarning("Classify error on ({Wiki}, {Title}, {WikiEditId}): {Error}",
                message.Wiki, message.Title, message.WikiEditId, enrResult.Error);
                return;
            }
            else
            {
                var firstTopic = enrResult.Value!.Topics.Count > 0 ? enrResult.Value.Topics[0] : null;
                var topicName = firstTopic?.TopicName ?? "<null>";
                var isDefault = string.Equals(topicName, "Uncategorized", StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "Classify success on ({Wiki}, {Title}, {WikiEditId}): Topics: {Count} FirstTopic={Topic} IsDefault={IsDefault}",
                    message.Wiki,
                    message.Title,
                    message.WikiEditId,
                    enrResult.Value.Topics.Count,
                    topicName,
                    isDefault);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error classify ({Wiki}, {Title}, {WikiEditId})",
                message.Wiki, message.Title, message.WikiEditId);
        }
    }
}
