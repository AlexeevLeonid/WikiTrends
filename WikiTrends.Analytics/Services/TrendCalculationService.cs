using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Kafka.Producer;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Analytics.Services;

public sealed class TrendCalculationService : ITrendCalculationService
{
    private readonly IClickHouseClient _clickHouseClient;
    private readonly IAnomalyDetectionService _anomalyDetectionService;
    private readonly IBaselineService _baselineService;
    private readonly IKafkaProducer<string, TrendUpdateEvent> _producer;
    private readonly AnalyticsOptions _options;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<TrendCalculationService> _logger;

    private static volatile bool _isSchemaInitialized = false;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    public TrendCalculationService(
        IClickHouseClient clickHouseClient,
        IAnomalyDetectionService anomalyDetectionService,
        IBaselineService baselineService,
        IKafkaProducer<string, TrendUpdateEvent> producer,
        IOptions<AnalyticsOptions> options,
        IOptions<TopicsOptions> topicsOptions,
        ILogger<TrendCalculationService> logger)
    {
        _clickHouseClient = clickHouseClient;
        _anomalyDetectionService = anomalyDetectionService;
        _baselineService = baselineService;
        _producer = producer;
        _options = options.Value;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    public async Task CalculateAndPublishAsync(CancellationToken ct = default)
    {
        //  1. Определить окно расчёта по _options (периоды LastHour/Last24Hours/Last7Days)
        //  2. Вычитать агрегаты из ClickHouse через _clickHouseClient.QueryTrendsAsync
        //  3. Для каждого топика получить baseline через _baselineService.GetBaselineAsync
        //  4. Посчитать anomaly score/percent change через _anomalyDetectionService
        //  5. Сформировать TrendUpdateEvent
        //  6. Опубликовать TrendUpdateEvent в Kafka через _producer
        //  7. Логировать статистику расчёта
        {
            _logger.LogInformation("Starting calculation cycle.");

            if (!_isSchemaInitialized)
            {
                await InitializeSchemaOnceAsync(ct);
            }

            var periods = Enum.GetValues<TrendPeriod>();

            foreach (var period in periods)
            {
                using var scope = _logger.BeginScope(new { Period = period });

                try
                {
                    var rawTrends = await _clickHouseClient.QueryTrendsAsync(period, ct);

                    if (rawTrends.Count == 0) continue;

                    rawTrends = rawTrends
                        .OrderByDescending(t => t.EditCount)
                        .Take(Math.Max(1, _options.TopTopicsLimit))
                        .ToList();

                    var topicInfo = await _clickHouseClient.GetTopicInfoAsync(rawTrends.Select(t => t.TopicId), ct);

                    var calculatedTrends = new ConcurrentBag<TopicTrend>();

                    await Parallel.ForEachAsync(rawTrends, new ParallelOptions
                    {
                        CancellationToken = ct,
                        MaxDegreeOfParallelism = Math.Max(1, _options.TrendCalculationParallelism)
                    },
                    async (trend, token) =>
                    {
                        var baseline = await _baselineService.GetBaselineAsync(trend.TopicId, token);
                        var detection = await _anomalyDetectionService.DetectAsync(trend, baseline, token);

                        // Если это не аномалия и скор маленький — не включаем в отчет, чтобы не раздувать JSON.
                        // (Например, показываем только то, что выросло на 20% или имеет высокий Z-Score)
                        if (detection.AnomalyScore < _options.MinAnomalyScoreToReport
                            && detection.ChangePercent < _options.MinChangePercentToReport)
                        {
                            return;
                        }

                        var topArticles = await _clickHouseClient.GetTopArticlesForTopicAsync(
                            trend.TopicId,
                            period,
                            Math.Max(1, _options.TopArticlesPerTopicLimit),
                            token);

                        var topicTrend = new TopicTrend
                        {
                            TopicId = trend.TopicId,
                            TopicName = topicInfo.TryGetValue(trend.TopicId, out var info)
                                && !string.IsNullOrWhiteSpace(info.Name)
                                    ? info.Name
                                    : $"topic-{trend.TopicId}",
                            EditCount = trend.EditCount,
                            AnomalyScore = detection.AnomalyScore,
                            ChangePercent = detection.ChangePercent,
                            TopArticles = topArticles
                        };

                        calculatedTrends.Add(topicTrend);
                    });

                    if (!calculatedTrends.IsEmpty)
                    {
                        var sortedTopics = calculatedTrends
                            .OrderByDescending(t => t.AnomalyScore)
                            .ToList();

                        var updateEvent = new TrendUpdateEvent
                        {
                            EventId = Guid.NewGuid().ToString(),
                            Period = period,
                            CalculatedAt = DateTimeOffset.UtcNow,
                            Topics = sortedTopics
                        };

                        await _producer.ProduceAsync(_topicsOptions.TrendUpdates,
                            period.ToString(),
                            updateEvent,
                            ct);

                        _logger.LogInformation("Published update for {Period} with {Count} topics.", period, sortedTopics.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to calculate/publish trends for period {Period}", period);
                }
            }
        }
    }

    private async Task InitializeSchemaOnceAsync(CancellationToken ct)
    {
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (!_isSchemaInitialized)
            {
                _logger.LogInformation("Initializing ClickHouse schema (trend calculation)");
                await _clickHouseClient.EnsureSchemaAsync(ct);
                _isSchemaInitialized = true;
                _logger.LogInformation("ClickHouse schema initialized successfully (trend calculation).");
            }
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
