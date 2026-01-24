using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.DataSources;
using WikiTrends.Aggregator.Handlers;
using WikiTrends.Aggregator.Services;
using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Handlers;
using WikiTrends.Analytics.Services;
using WikiTrends.Classifier.Caching;
using WikiTrends.Classifier.Data.Repositories;
using WikiTrends.Classifier.Handlers;
using WikiTrends.Classifier.Services;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Handlers;
using WikiTrends.Gateway.Services;
using WikiTrends.Scheduler.Services;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;
using WikiTrends.Classifier.Configuration;

namespace WikiTrends.Tests.NotImplemented;

[Trait("Category", "Legacy")]
public sealed class NotImplementedLogicTests
{
    [Fact]
    public async Task Enricher_RawEditHandler_HandleAsync_ThrowsNotImplemented()
    {
        var handler = new RawEditHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<RawEditHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(CreateRawEdit(), CancellationToken.None));
    }

    [Fact]
    public async Task Classifier_EnrichedEditHandler_HandleAsync_ThrowsNotImplemented()
    {
        var handler = new EnrichedEditHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<EnrichedEditHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(CreateEnrichedEdit(), CancellationToken.None));
    }

    [Fact]
    public async Task Analytics_ClassifiedEditHandler_HandleAsync_ThrowsNotImplemented()
    {
        var handler = new ClassifiedEditHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<ClassifiedEditHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(CreateClassifiedEdit(), CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_CommandHandler_HandleAsync_Recalculate_ThrowsNotImplemented()
    {
        var handler = new CommandHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<CommandHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(new RecalculateBaselineCommand { TopicId = null, RequestedAt = DateTimeOffset.UtcNow }, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_CommandHandler_HandleAsync_InvalidateCache_ThrowsNotImplemented()
    {
        var handler = new CommandHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<CommandHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(new InvalidateCacheCommand { CacheKey = "k", RequestedAt = DateTimeOffset.UtcNow }, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_TrendUpdateHandler_HandleAsync_ThrowsNotImplemented()
    {
        var handler = new TrendUpdateHandler(Mock.Of<IServiceScopeFactory>(), NullLogger<TrendUpdateHandler>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(new TrendUpdateEvent
        {
            EventId = "evt-1",
            Period = TrendPeriod.Last24Hours,
            Topics = Array.Empty<TopicTrend>(),
            CalculatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_AggregationService_GetTrendsAsync_ThrowsNotImplemented()
    {
        var service = new AggregationService(
            Mock.Of<ICacheService>(),
            Mock.Of<IAnalyticsDataSource>(),
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetTrendsAsync(new GetTrendsRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_AggregationService_GetTopicDetailsAsync_ThrowsNotImplemented()
    {
        var service = new AggregationService(
            Mock.Of<ICacheService>(),
            Mock.Of<IAnalyticsDataSource>(),
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetTopicDetailsAsync(1, TrendPeriod.Last24Hours, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_AggregationService_GetClustersAsync_ThrowsNotImplemented()
    {
        var service = new AggregationService(
            Mock.Of<ICacheService>(),
            Mock.Of<IAnalyticsDataSource>(),
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetClustersAsync(TrendPeriod.Last24Hours, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_AggregationService_HandleTrendUpdateAsync_ThrowsNotImplemented()
    {
        var service = new AggregationService(
            Mock.Of<ICacheService>(),
            Mock.Of<IAnalyticsDataSource>(),
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => service.HandleTrendUpdateAsync(new TrendUpdateEvent
        {
            EventId = "evt-1",
            Period = TrendPeriod.Last24Hours,
            Topics = Array.Empty<TopicTrend>(),
            CalculatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_RedisCacheService_GetStringAsync_ThrowsNotImplemented()
    {
        var svc = new RedisCacheService(Mock.Of<IDistributedCache>(), NullLogger<RedisCacheService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => svc.GetStringAsync("k", CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_RedisCacheService_SetStringAsync_ThrowsNotImplemented()
    {
        var svc = new RedisCacheService(Mock.Of<IDistributedCache>(), NullLogger<RedisCacheService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => svc.SetStringAsync("k", "v", TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    [Fact]
    public async Task Aggregator_RedisCacheService_RemoveAsync_ThrowsNotImplemented()
    {
        var svc = new RedisCacheService(Mock.Of<IDistributedCache>(), NullLogger<RedisCacheService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => svc.RemoveAsync("k", CancellationToken.None));
    }

    [Fact]
    public async Task Scheduler_CommandPublisher_PublishRecalculateBaselineAsync_ThrowsNotImplemented()
    {
        var publisher = new CommandPublisher(
            Mock.Of<IKafkaProducer<string, RecalculateBaselineCommand>>(),
            Mock.Of<IKafkaProducer<string, InvalidateCacheCommand>>(),
            Options.Create(new TopicsOptions()),
            NullLogger<CommandPublisher>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => publisher.PublishRecalculateBaselineAsync(new RecalculateBaselineCommand { TopicId = null, RequestedAt = DateTimeOffset.UtcNow }, CancellationToken.None));
    }

    [Fact]
    public async Task Scheduler_CommandPublisher_PublishInvalidateCacheAsync_ThrowsNotImplemented()
    {
        var publisher = new CommandPublisher(
            Mock.Of<IKafkaProducer<string, RecalculateBaselineCommand>>(),
            Mock.Of<IKafkaProducer<string, InvalidateCacheCommand>>(),
            Options.Create(new TopicsOptions()),
            NullLogger<CommandPublisher>.Instance);

        await Assert.ThrowsAsync<NotImplementedException>(() => publisher.PublishInvalidateCacheAsync(new InvalidateCacheCommand { CacheKey = "k", RequestedAt = DateTimeOffset.UtcNow }, CancellationToken.None));
    }

    [Fact]
    public async Task Gateway_TopicService_GetTopicDetailsAsync_ThrowsNotImplemented()
    {
        var service = new TopicService(
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new ServiceUrlsOptions()),
            NullLogger<TopicService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetTopicDetailsAsync(1, TrendPeriod.Last24Hours, CancellationToken.None));
    }

    [Fact]
    public async Task Gateway_TrendService_GetTrendsAsync_ThrowsNotImplemented()
    {
        var service = new TrendService(
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new ServiceUrlsOptions()),
            NullLogger<TrendService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetTrendsAsync(new GetTrendsRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Gateway_TrendService_GetClustersAsync_ThrowsNotImplemented()
    {
        var service = new TrendService(
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new ServiceUrlsOptions()),
            NullLogger<TrendService>.Instance);
        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetClustersAsync(TrendPeriod.Last24Hours, CancellationToken.None));
    }

    [Fact(Skip = "Analytics logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Analytics_EventIngestionService_IngestAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Analytics logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Analytics_BaselineService_GetBaselineAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Analytics logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Analytics_BaselineService_RecalculateBaselinesAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Analytics logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Analytics_AnomalyDetectionService_DetectAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Analytics logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Analytics_TrendCalculationService_CalculateAndPublishAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Classifier logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Classifier_ClassificationService_ClassifyAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Classifier logic is implemented; this legacy NotImplemented test is obsolete.")]
    public Task Classifier_TopicMappingService_MapTopicsAsync_ThrowsNotImplemented()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Classifier cache is implemented; this legacy NotImplemented test is obsolete.")]
    public void Classifier_WikidataMemoryCache_TryGet_ThrowsNotImplemented()
    {
        var cache = new WikidataMemoryCache(Mock.Of<IMemoryCache>(), NullLogger<WikidataMemoryCache>.Instance);
        Assert.Throws<NotImplementedException>(() => cache.TryGet("k", out _));
    }

    [Fact(Skip = "Classifier cache is implemented; this legacy NotImplemented test is obsolete.")]
    public void Classifier_WikidataMemoryCache_Set_ThrowsNotImplemented()
    {
        var cache = new WikidataMemoryCache(Mock.Of<IMemoryCache>(), NullLogger<WikidataMemoryCache>.Instance);
        Assert.Throws<NotImplementedException>(() => cache.Set("k", new WikiTrends.Classifier.Models.WikidataResponse
        {
            Entity = null,
            Claims = Array.Empty<string>()
        }, TimeSpan.FromMinutes(1)));
    }

    [Fact(Skip = "Classifier cache is implemented; this legacy NotImplemented test is obsolete.")]
    public void Classifier_WikidataMemoryCache_Remove_ThrowsNotImplemented()
    {
        var cache = new WikidataMemoryCache(Mock.Of<IMemoryCache>(), NullLogger<WikidataMemoryCache>.Instance);
        Assert.Throws<NotImplementedException>(() => cache.Remove("k"));
    }

    private static RawEditEvent CreateRawEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        PageId = 42,
        Title = "T",
        Wiki = "enwiki",
        User = "u",
        IsBot = false,
        IsMinor = false,
        IsNew = false,
        OldLength = 1,
        NewLength = 2,
        Timestamp = DateTimeOffset.UtcNow,
        CollectedAt = DateTimeOffset.UtcNow
    };

    private static EnrichedEditEvent CreateEnrichedEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        ArticleId = 100,
        PageId = 42,
        Title = "T",
        Wiki = "enwiki",
        Extract = "x",
        Categories = new List<string> { "C" },
        LinkedArticles = new List<string>(),
        IsBot = false,
        DiffSize = 1,
        Timestamp = DateTimeOffset.UtcNow,
        EnrichedAt = DateTimeOffset.UtcNow
    };

    private static ClassifiedEditEvent CreateClassifiedEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        ArticleId = 100,
        Title = "T",
        Wiki = "enwiki",
        Topics = new List<TopicScore>
        {
            new()
            {
                TopicId = 1,
                TopicName = "Topic",
                TopicPath = "Root/Topic",
                Confidence = 0.5f
            }
        },
        Embedding = Array.Empty<float>(),
        Timestamp = DateTimeOffset.UtcNow,
        ClassifiedAt = DateTimeOffset.UtcNow
    };
}
