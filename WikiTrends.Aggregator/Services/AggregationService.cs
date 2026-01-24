using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.DataSources;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Aggregator.Services;

public sealed class AggregationService : IAggregationService
{
    private readonly ICacheService _cache;
    private readonly IAnalyticsDataSource _analytics;
    private readonly IClassifierDataSource _classifier;
    private readonly IEnricherDataSource _enricher;
    private readonly ILogger<AggregationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AggregationService(
        ICacheService cache,
        IAnalyticsDataSource analytics,
        IClassifierDataSource classifier,
        IEnricherDataSource enricher,
        ILogger<AggregationService> logger)
    {
        _cache = cache;
        _analytics = analytics;
        _classifier = classifier;
        _enricher = enricher;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<Result<TrendsResponse>> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать ключ кэша через CacheKeys.GetTrendsKey(request.Period)
        // TODO: 2. Попробовать получить JSON из _cache
        // TODO: 3. Если кэш hit — десериализовать TrendsResponse и вернуть Result.Success
        // TODO: 4. Если miss — запросить через _analytics.GetTrendsAsync(request, ct)
        // TODO: 5. Если источник вернул ошибку — вернуть Result.Failure
        // TODO: 6. Сериализовать и записать в кэш с TTL
        // TODO: 7. Вернуть Result.Success
        if (request == null)
        {
            return Result<TrendsResponse>.Failure("Request is null");
        }

        var key = CacheKeys.GetTrendsKey(request.Period);

        var cachedString = await _cache.GetStringAsync(key, ct);
        if (!string.IsNullOrEmpty(cachedString))
        {
            try
            {
                var cachedResult = JsonSerializer.Deserialize<TrendsResponse>(cachedString, _jsonOptions);
                if (cachedResult != null)
                {
                    return Result<TrendsResponse>.Success(cachedResult);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialization TrendsResponse error {Error}", ex.Message);
            }
        }

        var getResult = await _analytics.GetTrendsAsync(request, ct);
        if (!getResult.IsSuccess)
        {
            _logger.LogWarning("Trends are not available from Analytics: {Error}", getResult.Error);
            return Result<TrendsResponse>.Success(new TrendsResponse
            {
                Period = request.Period,
                GeneratedAt = DateTimeOffset.UtcNow,
                Topics = Array.Empty<TopicTrendDto>()
            });
        }

        var jsonToCache = JsonSerializer.Serialize(getResult.Value, _jsonOptions);
        await _cache.SetStringAsync(key, jsonToCache, TimeSpan.FromSeconds(30), ct);

        return Result<TrendsResponse>.Success(getResult.Value!);
    }

    public async Task<Result<TopicDetailResponse>> GetTopicDetailsAsync(int topicId, TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать ключ кэша через CacheKeys.GetTopicDetailsKey(topicId, period)
        // TODO: 2. Попробовать получить JSON из _cache
        // TODO: 3. Если miss — запросить данные через _analytics.GetTopicDetailsAsync(topicId, period, ct)
        // TODO: 4. Опционально обогатить ответ (например, через _enricher/_classifier)
        // TODO: 5. Закэшировать и вернуть Result.Success
        if (topicId <= 0)
        {
            return Result<TopicDetailResponse>.Failure("TopicId must be positive");
        }

        var key = CacheKeys.GetTopicDetailsKey(topicId, period);

        var cachedString = await _cache.GetStringAsync(key, ct);
        if (!string.IsNullOrEmpty(cachedString))
        {
            try
            {
                var cachedResult = JsonSerializer.Deserialize<TopicDetailResponse>(cachedString, _jsonOptions);
                if (cachedResult != null)
                {
                    return Result<TopicDetailResponse>.Success(cachedResult);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialization TopicDetailResponse error {Error}", ex.Message);
            }
        }

        var detailsFromCache = await TryBuildTopicDetailsFromTrendsCacheAsync(topicId, period, ct);
        if (detailsFromCache != null)
        {
            var json = JsonSerializer.Serialize(detailsFromCache, _jsonOptions);
            await _cache.SetStringAsync(key, json, TimeSpan.FromSeconds(30), ct);
            return Result<TopicDetailResponse>.Success(detailsFromCache);
        }

        var getResult = await _analytics.GetTopicDetailsAsync(topicId, period, ct);
        if (!getResult.IsSuccess)
        {
            _logger.LogWarning("Topic details are not available from Analytics. TopicId={TopicId}. Period={Period}. Error={Error}",
                topicId,
                period,
                getResult.Error);

            var fallback = new TopicDetailResponse
            {
                TopicId = topicId,
                Name = $"topic-{topicId}",
                Path = $"topic-{topicId}",
                Stats = new TopicStatsDto
                {
                    EditCountLastHour = 0,
                    EditCountLast24Hours = 0,
                    EditCountLast7Days = 0,
                    BaselineDaily = 0,
                    AnomalyScore = 0
                },
                Articles = Array.Empty<ArticleDto>(),
                RelatedTopics = Array.Empty<RelatedTopicDto>()
            };

            var jsonFallback = JsonSerializer.Serialize(fallback, _jsonOptions);
            await _cache.SetStringAsync(key, jsonFallback, TimeSpan.FromSeconds(5), ct);

            return Result<TopicDetailResponse>.Success(fallback);
        }

        var jsonToCache = JsonSerializer.Serialize(getResult.Value, _jsonOptions);
        await _cache.SetStringAsync(key, jsonToCache, TimeSpan.FromSeconds(30), ct);

        return Result<TopicDetailResponse>.Success(getResult.Value!);
    }

    public async Task<Result<ClusterResponse>> GetClustersAsync(TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Получить кластеры из Analytics через _analytics.GetClustersAsync(period, ct)
        // TODO: 2. Опционально обогатить данные
        // TODO: 3. Вернуть Result.Success/Failure
        try
        {
            var clusters = await _analytics.GetClustersAsync(period, ct);
            if (clusters.IsSuccess)
            {
                return clusters;
            }

            _logger.LogWarning("Clusters are not available from Analytics: {Error}", clusters.Error);
            return Result<ClusterResponse>.Success(new ClusterResponse
            {
                Clusters = Array.Empty<ClusterDto>(),
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get clusters.");
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }

    public async Task HandleTrendUpdateAsync(TrendUpdateEvent message, CancellationToken ct = default)
    {
        // TODO: 1. На основе message.Period обновить кэш трендов
        // TODO: 2. Сериализовать message.Topics/или полный TrendsResponse и записать в Redis
        // TODO: 3. Обработать ошибки и логирование
        if (message == null)
        {
            _logger.LogWarning("Received null TrendUpdateEvent in aggregation service.");
            return;
        }

        var key = CacheKeys.GetTrendsKey(message.Period);

        try
        {
            var response = new TrendsResponse
            {
                Period = message.Period,
                GeneratedAt = message.CalculatedAt,
                Topics = message.Topics.Select(t => new TopicTrendDto
                {
                    TopicId = t.TopicId,
                    Name = t.TopicName,
                    Path = t.TopicName,
                    EditCount = t.EditCount,
                    AnomalyScore = t.AnomalyScore,
                    ChangePercent = t.ChangePercent,
                    Direction = t.ChangePercent switch
                    {
                        > 0.05f => TrendDirection.Rising,
                        < -0.05f => TrendDirection.Falling,
                        _ => TrendDirection.Stable
                    },
                    TopArticles = t.TopArticles.Select(a => new ArticleDto
                    {
                        Id = a.ArticleId,
                        WikiPageId = 0,
                        Title = a.Title,
                        Wiki = "unknown",
                        Extract = null,
                        WikiUrl = string.Empty,
                        EditCount = a.EditCount,
                        UniqueEditors = a.UniqueEditors,
                        LastEditAt = message.CalculatedAt
                    }).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await _cache.SetStringAsync(key, json, TimeSpan.FromSeconds(30), ct);

            _logger.LogInformation(
                "Updated trends cache for {Period}. Topics: {Count}",
                message.Period,
                response.Topics.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update trends cache for {Period}", message.Period);
        }
    }

    private async Task<TopicDetailResponse?> TryBuildTopicDetailsFromTrendsCacheAsync(int topicId, TrendPeriod period, CancellationToken ct)
    {
        var requested = await TryGetTrendsFromCacheAsync(period, ct);
        if (requested == null)
        {
            return null;
        }

        var requestedTopic = requested.Topics.FirstOrDefault(t => t.TopicId == topicId);
        if (requestedTopic == null)
        {
            return null;
        }

        var lastHour = await TryGetTrendsFromCacheAsync(TrendPeriod.LastHour, ct);
        var last24 = await TryGetTrendsFromCacheAsync(TrendPeriod.Last24Hours, ct);
        var last7 = await TryGetTrendsFromCacheAsync(TrendPeriod.Last7Days, ct);

        int countHour = lastHour?.Topics.FirstOrDefault(t => t.TopicId == topicId)?.EditCount ?? 0;
        int count24 = last24?.Topics.FirstOrDefault(t => t.TopicId == topicId)?.EditCount ?? 0;
        int count7 = last7?.Topics.FirstOrDefault(t => t.TopicId == topicId)?.EditCount ?? 0;

        return new TopicDetailResponse
        {
            TopicId = requestedTopic.TopicId,
            Name = requestedTopic.Name,
            Path = requestedTopic.Path,
            Stats = new TopicStatsDto
            {
                EditCountLastHour = countHour,
                EditCountLast24Hours = count24,
                EditCountLast7Days = count7,
                BaselineDaily = 0,
                AnomalyScore = requestedTopic.AnomalyScore
            },
            Articles = requestedTopic.TopArticles,
            RelatedTopics = Array.Empty<RelatedTopicDto>()
        };
    }

    private async Task<TrendsResponse?> TryGetTrendsFromCacheAsync(TrendPeriod period, CancellationToken ct)
    {
        var key = CacheKeys.GetTrendsKey(period);
        var cachedString = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrEmpty(cachedString))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrendsResponse>(cachedString, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached TrendsResponse for {Period}", period);
            return null;
        }
    }
}
