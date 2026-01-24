using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Configuration;
using WikiTrends.Enricher.Data.Entities;
using WikiTrends.Enricher.Data.Repositories;
using WikiTrends.Enricher.Models;
using WikiTrends.Enricher.Services;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;

namespace WikiTrends.Tests.Services.Enricher;

public sealed class EnrichmentServiceTests
{
    [Fact]
    public async Task EnrichAsync_WhenEditEventIsNull_ReturnsFailure_AndDoesNotCallDependencies()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var result = await service.EnrichAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Edit event is null", result.Error);

        wikipedia.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrichAsync_WhenTitleMissing_ReturnsFailure()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { Title = "" };

        var result = await service.EnrichAsync(edit);

        Assert.False(result.IsSuccess);
        Assert.Equal("Title is required", result.Error);

        wikipedia.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrichAsync_WhenWikiMissing_ReturnsFailure()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { Wiki = "" };

        var result = await service.EnrichAsync(edit);

        Assert.False(result.IsSuccess);
        Assert.Equal("Wiki is required", result.Error);

        wikipedia.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrichAsync_WhenPageIdNotPositive_AndApiDoesNotReturnValidPageId_ReturnsFailure()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        wikipedia
            .Setup(w => w.GetPageDataAsync("T", "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WikiTrends.Contracts.Common.Result<WikipediaApiResponse>.Success(new WikipediaApiResponse
            {
                Extract = "x",
                Categories = Array.Empty<string>(),
                LinkedArticles = Array.Empty<string>(),
                PageId = 0,
            }));

        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { PageId = 0 };

        var result = await service.EnrichAsync(edit);

        Assert.False(result.IsSuccess);
        Assert.Equal("Wikipedia API did not return valid PageId", result.Error);

        wikipedia.VerifyAll();
        repo.VerifyNoOtherCalls();
        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrichAsync_WhenArticleIsFresh_UsesCache_AndPublishesToKafka()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);

        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByWikiPageIdAsync(42, "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArticleEntity
            {
                Id = 100,
                WikiPageId = 42,
                Title = "Test",
                Wiki = "enwiki",
                Extract = "cached",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                LastEnrichedAt = DateTimeOffset.UtcNow,
                Categories = new List<CategoryEntity> { new() { Id = 1, Name = "Cat", ArticleId = 100 } }
            });

        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);
        producer
            .Setup(p => p.ProduceAsync(
                "wiki.enriched",
                "42",
                It.IsAny<EnrichedEditEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, EnrichedEditEvent>());

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { PageId = 42, Wiki = "enwiki", Title = "Test" };

        var result = await service.EnrichAsync(edit);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100, result.Value!.ArticleId);
        Assert.Equal("Test", result.Value.Title);
        Assert.Single(result.Value.Categories);
        Assert.Empty(result.Value.LinkedArticles);

        wikipedia.VerifyNoOtherCalls();
        repo.Verify(r => r.GetByWikiPageIdAsync(42, "enwiki", It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
        producer.VerifyAll();
    }

    [Fact]
    public async Task EnrichAsync_WhenCacheMiss_CallsWikipedia_Upserts_ReplacesCategories_AndPublishes()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        wikipedia
            .Setup(w => w.GetPageDataAsync("Test", "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WikiTrends.Contracts.Common.Result<WikipediaApiResponse>.Success(new WikipediaApiResponse
            {
                Extract = "from-api",
                Categories = new[] { "C1", "C2" },
                LinkedArticles = new[] { "L1", "L2" },
                PageId = 42,
            }));

        var articleAfterUpsert = new ArticleEntity
        {
            Id = 100,
            WikiPageId = 42,
            Title = "Test",
            Wiki = "enwiki",
            Extract = "from-api",
            CreatedAt = DateTimeOffset.UtcNow,
            LastEnrichedAt = DateTimeOffset.UtcNow
        };

        var articleWithCategories = new ArticleEntity
        {
            Id = 100,
            WikiPageId = 42,
            Title = "Test",
            Wiki = "enwiki",
            Extract = "from-api",
            CreatedAt = DateTimeOffset.UtcNow,
            LastEnrichedAt = DateTimeOffset.UtcNow,
            Categories = new List<CategoryEntity>
            {
                new() { Id = 1, Name = "C1", ArticleId = 100 },
                new() { Id = 2, Name = "C2", ArticleId = 100 }
            }
        };

        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        repo.SetupSequence(r => r.GetByWikiPageIdAsync(42, "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArticleEntity?)null)
            .ReturnsAsync(articleWithCategories);

        repo.Setup(r => r.UpsertAsync(It.IsAny<ArticleEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(articleAfterUpsert);

        repo.Setup(r => r.ReplaceCategoriesAsync(100, It.Is<IReadOnlyList<string>>(c => c.SequenceEqual(new[] { "C1", "C2" })), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);
        producer
            .Setup(p => p.ProduceAsync(
                "wiki.enriched",
                "42",
                It.IsAny<EnrichedEditEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, EnrichedEditEvent>());

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { PageId = 42, Wiki = "enwiki", Title = "Test" };

        var result = await service.EnrichAsync(edit);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100, result.Value!.ArticleId);
        Assert.Equal("from-api", result.Value.Extract);
        Assert.Equal(2, result.Value.Categories.Count);
        Assert.Equal(2, result.Value.LinkedArticles.Count);

        wikipedia.VerifyAll();
        repo.VerifyAll();
        producer.VerifyAll();
    }

    [Fact]
    public async Task EnrichAsync_WhenWikipediaApiFails_ReturnsFailure_AndDoesNotProduce()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);
        wikipedia
            .Setup(w => w.GetPageDataAsync("Test", "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WikiTrends.Contracts.Common.Result<WikipediaApiResponse>.Failure("api-failed"));

        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByWikiPageIdAsync(42, "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArticleEntity?)null);

        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { PageId = 42, Wiki = "enwiki", Title = "Test" };

        var result = await service.EnrichAsync(edit);

        Assert.False(result.IsSuccess);
        Assert.Equal("api-failed", result.Error);

        wikipedia.VerifyAll();
        repo.VerifyAll();
        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnrichAsync_WhenKafkaPublishThrows_ReturnsFailure()
    {
        var wikipedia = new Mock<IWikipediaApiClient>(MockBehavior.Strict);

        var repo = new Mock<IArticleRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByWikiPageIdAsync(42, "enwiki", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArticleEntity
            {
                Id = 100,
                WikiPageId = 42,
                Title = "Test",
                Wiki = "enwiki",
                Extract = "cached",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                LastEnrichedAt = DateTimeOffset.UtcNow,
                Categories = new List<CategoryEntity>()
            });

        var producer = new Mock<IKafkaProducer<string, EnrichedEditEvent>>(MockBehavior.Strict);
        producer
            .Setup(p => p.ProduceAsync(
                "wiki.enriched",
                "42",
                It.IsAny<EnrichedEditEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = CreateService(wikipedia.Object, repo.Object, producer.Object);

        var edit = CreateValidRawEditEvent() with { PageId = 42, Wiki = "enwiki", Title = "Test" };

        var result = await service.EnrichAsync(edit);

        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to publish to Kafka", result.Error);

        wikipedia.VerifyNoOtherCalls();
        repo.VerifyAll();
        producer.VerifyAll();
    }

    private static EnrichmentService CreateService(
        IWikipediaApiClient wikipediaApiClient,
        IArticleRepository articleRepository,
        IKafkaProducer<string, EnrichedEditEvent> producer)
    {
        var enricherOptions = Options.Create(new EnricherOptions { ArticleCacheHours = 4 });
        var topicsOptions = Options.Create(new TopicsOptions
        {
            RawEdits = "wiki.raw-edits",
            EnrichedEdits = "wiki.enriched",
            ClassifiedEdits = "wiki.classified",
            TrendUpdates = "wiki.trend-updates",
            RecalculateBaselineCommands = "wiki.commands.recalculate-baseline",
            InvalidateCacheCommands = "wiki.commands.invalidate-cache"
        });

        return new EnrichmentService(
            wikipediaApiClient,
            articleRepository,
            producer,
            enricherOptions,
            topicsOptions,
            NullLogger<EnrichmentService>.Instance);
    }

    private static RawEditEvent CreateValidRawEditEvent()
    {
        return new RawEditEvent
        {
            EventId = "evt-1",
            WikiEditId = 1,
            PageId = 1,
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
    }
}
