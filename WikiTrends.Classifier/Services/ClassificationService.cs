using Microsoft.Extensions.Options;
using WikiTrends.Classifier.Caching;
using WikiTrends.Classifier.Configuration;
using WikiTrends.Classifier.Models;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;

namespace WikiTrends.Classifier.Services;

public sealed class ClassificationService : IClassificationService
{
    private readonly ITopicResolverService _topicResolverService;
    private readonly IKafkaProducer<string, ClassifiedEditEvent> _producer;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<ClassificationService> _logger;

    public ClassificationService(
        ITopicResolverService topicResolverService,
        IKafkaProducer<string, ClassifiedEditEvent> producer,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<ClassificationService> logger)
    {
        _topicResolverService = topicResolverService;
        _producer = producer;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    public async Task<Result<ClassifiedEditEvent>> ClassifyAsync(EnrichedEditEvent editEvent, CancellationToken ct = default)
    {

        
        //  1. Провалидировать входной editEvent (не null, ArticleId/PageId/Title/Wiki)
        //  2. Попытаться получить данные Wikidata из кэша (ключ по Wiki+Title/PageId)
        //  3. Если в кэше нет — запросить через _wikidataClient.GetEntityAsync(title, wiki, ct)
        //  4. Если Wikidata вернул ошибку — вернуть Result.Failure(error)
        //  5. Сохранить результат в кэш через _wikidataCache
        //  6. Получить topics через _topicMappingService.MapTopicsAsync(editEvent, ct)
        //  7. Если mapping неуспешный — вернуть Result.Failure(error)
        //  8. Сформировать embedding (пока заглушка/вызов внешнего сервиса)
        //  9. Собрать ClassifiedEditEvent (ClassifiedAt = UtcNow)
        //  10. Опубликовать событие в Kafka (topic "wiki.classified", key = ArticleId/PageId)
        //  11. Вернуть Result.Success(classifiedEvent)
        if (editEvent == null)
        {
            return Result<ClassifiedEditEvent>.Failure("Edit event is null");
        }

        if (string.IsNullOrWhiteSpace(editEvent.Wiki) || string.IsNullOrWhiteSpace(editEvent.Title))
        {
            return Result<ClassifiedEditEvent>.Failure("Edit event is missing required fields");
        }

        var lang = editEvent.Wiki.EndsWith("wiki", StringComparison.OrdinalIgnoreCase)
            ? editEvent.Wiki[..^4]
            : editEvent.Wiki;

        var resolved = await _topicResolverService.ResolveAndSaveTopicAsync(editEvent.ArticleId, editEvent.Title, lang, ct);
        if (!resolved.IsSuccess)
        {
            return Result<ClassifiedEditEvent>.Failure($"Topic resolve error: {resolved.Error}");
        }

        var topicEntity = resolved.Value!.Topic;
        if (topicEntity == null)
        {
            return Result<ClassifiedEditEvent>.Failure("Resolved topic was not loaded");
        }

        var topics = new List<TopicScore>
        {
            new()
            {
                TopicId = (int)topicEntity.Id,
                TopicName = topicEntity.Name,
                TopicPath = topicEntity.Path,
                Confidence = resolved.Value!.Confidence
            }
        };
        var classifiedEvent = new ClassifiedEditEvent
        {
            EventId = editEvent.EventId,
            WikiEditId = editEvent.WikiEditId,
            ArticleId = editEvent.ArticleId,
            Title = editEvent.Title,
            Wiki = editEvent.Wiki,
            Topics = topics,
            Embedding = [],
            Timestamp = editEvent.Timestamp,
            ClassifiedAt = DateTimeOffset.UtcNow,
        };
        try
        {
            await _producer.ProduceAsync(
                _topicsOptions.ClassifiedEdits,
                classifiedEvent.ArticleId.ToString(),
                classifiedEvent,
                ct);
        }
        catch (Exception ex)
        {
            return Result<ClassifiedEditEvent>.Failure($"Produce ClassifiedEvent Error: {ex.Message}");
        }
        return Result<ClassifiedEditEvent>.Success(classifiedEvent);
    }
}
