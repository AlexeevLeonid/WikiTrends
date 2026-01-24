using System.Reflection.Metadata.Ecma335;
using WikiTrends.Classifier.Data.Entities;
using WikiTrends.Classifier.Data.Repositories;
using WikiTrends.Classifier.Models;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Classifier.Services;

public sealed class TopicMappingService : ITopicMappingService
{
    private readonly ITopicRepository _topicRepository;
    private readonly IArticleTopicRepository _articleTopicRepository;
    private readonly IWikidataClient _wikidataClient;
    private readonly ILogger<TopicMappingService> _logger;

    private const float ConfidenceThreshold = 0.3f;

    public TopicMappingService(
        ITopicRepository topicRepository,
        IArticleTopicRepository articleTopicRepository,
        IWikidataClient wikidataClient,
        ILogger<TopicMappingService> logger)
    {
        _topicRepository = topicRepository;
        _articleTopicRepository = articleTopicRepository;
        _wikidataClient = wikidataClient;
        _logger = logger;
    }


    public async Task<Result<IReadOnlyList<TopicScore>>> MapTopicsAsync(EnrichedEditEvent editEvent, WikidataResponse? wikidata, CancellationToken ct = default)
    {

        //  1. Провалидировать входной editEvent
        //  2. Загрузить справочник тем из БД через _topicRepository
        //  3. Сопоставить категории/linkedArticles/extract к темам (rule-based / model)
        //  4. Получить/обновить связи article->topics через _articleTopicRepository
        //  5. Вернуть Result.Success(topics)
        //  6. В случае ошибок вернуть Result.Failure("...")
        // 1. Провалидировать входной editEvent
        if (editEvent is null)
            return Result<IReadOnlyList<TopicScore>>.Failure("EditEvent cannot be null.");

        if (editEvent.ArticleId <= 0)
            return Result<IReadOnlyList<TopicScore>>.Failure($"Invalid ArticleId: {editEvent.ArticleId}");

        if (wikidata?.Entity == null)
        {
            return Result<IReadOnlyList<TopicScore>>.Success(Array.Empty<TopicScore>());
        }

        var instanceOf = wikidata.Entity.InstanceOf
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (instanceOf.Length == 0)
        {
            return Result<IReadOnlyList<TopicScore>>.Success(Array.Empty<TopicScore>());
        }

        try
        {
            var labelsResult = await _wikidataClient.GetLabelsAsync(instanceOf, editEvent.Wiki, ct);
            if (!labelsResult.IsSuccess)
            {
                return Result<IReadOnlyList<TopicScore>>.Failure(labelsResult.Error ?? "Wikidata labels request failed");
            }

            var labels = labelsResult.Value!;

            var mappingsToSave = new List<ArticleTopicEntity>();
            var resultScores = new List<TopicScore>();

            var baseConfidence = 0.8f;
            var step = 0.05f;
            var index = 0;

            foreach (var qid in instanceOf)
            {
                labels.TryGetValue(qid, out var label);
                label = string.IsNullOrWhiteSpace(label) ? qid : label;

                var topic = await _topicRepository.UpsertAsync(new TopicEntity
                {
                    Name = label!,
                    Path = $"Wikidata/P31/{qid}"
                }, ct);

                var confidence = Math.Max(ConfidenceThreshold, baseConfidence - (index * step));
                index++;

                mappingsToSave.Add(new ArticleTopicEntity
                {
                    ArticleId = editEvent.ArticleId,
                    TopicId = topic.Id,
                    Confidence = confidence
                });

                resultScores.Add(new TopicScore
                {
                    TopicId = (int)topic.Id,
                    TopicName = topic.Name,
                    TopicPath = topic.Path,
                    Confidence = confidence
                });
            }

            mappingsToSave = mappingsToSave.OrderByDescending(x => x.Confidence).ToList();
            resultScores = resultScores.OrderByDescending(x => x.Confidence).ToList();

            await _articleTopicRepository.ReplaceAsync(editEvent.ArticleId, mappingsToSave, ct);

            _logger.LogInformation(
                "Updated Wikidata topics for Article {ArticleId}. Entity={EntityId}. Topics={Count}",
                editEvent.ArticleId,
                wikidata.Entity.Id,
                mappingsToSave.Count);

            return Result<IReadOnlyList<TopicScore>>.Success(resultScores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to map Wikidata topics for Article {ArticleId}", editEvent.ArticleId);
            return Result<IReadOnlyList<TopicScore>>.Failure($"Wikidata topic mapping failed: {ex.Message}");
        }
    }
}
