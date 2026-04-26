using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Generic;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Data.Entities;
using WikiTrends.Enricher.Data.Repositories;
using WikiTrends.Enricher.Configuration;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;

namespace WikiTrends.Enricher.Services;

public sealed class EnrichmentService : IEnrichmentService
{
    private readonly IWikipediaApiClient _wikipediaApiClient;
    private readonly IArticleRepository _articleRepository;
    private readonly IKafkaProducer<string, EnrichedEditEvent> _producer;
    private readonly EnricherOptions _enricherOptions;
    private readonly TopicsOptions _topicsOptions;
    private readonly ILogger<EnrichmentService> _logger;

    public EnrichmentService(
        IWikipediaApiClient wikipediaApiClient,
        IArticleRepository articleRepository,
        IKafkaProducer<string, EnrichedEditEvent> producer,
        Microsoft.Extensions.Options.IOptions<EnricherOptions> enricherOptions,
        Microsoft.Extensions.Options.IOptions<TopicsOptions> topicsOptions,
        ILogger<EnrichmentService> logger)
    {
        _wikipediaApiClient = wikipediaApiClient;
        _articleRepository = articleRepository;
        _producer = producer;
        _enricherOptions = enricherOptions.Value;
        _topicsOptions = topicsOptions.Value;
        _logger = logger;
    }

    public async Task<Result<EnrichedEditEvent>> EnrichAsync(RawEditEvent editEvent, CancellationToken ct = default)
    {
        if (editEvent == null)
        {
            return Result<EnrichedEditEvent>.Failure("Edit event is null");
        }

        if (string.IsNullOrEmpty(editEvent.Title))
        {
            return Result<EnrichedEditEvent>.Failure("Title is required");
        }

        if (string.IsNullOrEmpty(editEvent.Wiki))
        {
            return Result<EnrichedEditEvent>.Failure("Wiki is required");
        }

        var pageId = editEvent.PageId;
        ArticleEntity? article = null;
        IReadOnlyList<string> linkedArticles = new List<string>();

        if (pageId > 0)
        {
            article = await _articleRepository.GetByWikiPageIdAsync(
                pageId,
                editEvent.Wiki,
                ct);
        }

        var cacheExpiration = DateTimeOffset.UtcNow.AddHours(-_enricherOptions.ArticleCacheHours);
        var needsRefresh = article == null || article.LastEnrichedAt < cacheExpiration;

        if (needsRefresh)
        {
            _logger.LogDebug(
                "Fetching data from Wikipedia API for {Title} in {Wiki}",
                editEvent.Title,
                editEvent.Wiki);

            var apiResult = await _wikipediaApiClient.GetPageDataAsync(
                editEvent.Title,
                editEvent.Wiki,
                ct);
            if (!apiResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Wikipedia API failed for {Title}: {Error}",
                    editEvent.Title,
                    apiResult.Error);

                return Result<EnrichedEditEvent>.Failure(
                    apiResult.Error ?? "Wikipedia API error");
            }

            var apiData = apiResult.Value!;
            if (pageId <= 0)
            {
                pageId = apiData.PageId;
            }

            if (pageId <= 0)
            {
                return Result<EnrichedEditEvent>.Failure("Wikipedia API did not return valid PageId");
            }

            linkedArticles = apiData.LinkedArticles;

            if (article == null)
            {
                article = new ArticleEntity
                {
                    WikiPageId = pageId,
                    Title = editEvent.Title,
                    Wiki = editEvent.Wiki,
                    Extract = Truncate(apiData.Extract, 4000),
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastEnrichedAt = DateTimeOffset.UtcNow
                };
            }
            else
            {
                article.Title = editEvent.Title;
                article.Extract = Truncate(apiData.Extract, 4000);
                article.LastEnrichedAt = DateTimeOffset.UtcNow;
            }

            article = await _articleRepository.UpsertAsync(article, ct);

            await _articleRepository.ReplaceCategoriesAsync(
                article.Id,
                apiData.Categories,
                ct);

            article = await _articleRepository.GetByWikiPageIdAsync(
                pageId,
                editEvent.Wiki,
                ct);

            if (article == null)
            {
                return Result<EnrichedEditEvent>.Failure("Article was not found after upsert");
            }

            _logger.LogInformation(
                "Enriched article {Title} from Wikipedia API. Categories: {Count}",
                editEvent.Title,
                apiData.Categories.Count);
        }
        else
        {
            _logger.LogDebug(
                "Using cached data for {Title}, last enriched at {LastEnrichedAt}",
                editEvent.Title,
                article!.LastEnrichedAt);
        }

        if (article == null)
        {
            return Result<EnrichedEditEvent>.Failure("Article was not available after enrichment");
        }

        var enrichedEvent = new EnrichedEditEvent
        {
            EventId = editEvent.EventId,
            WikiEditId = editEvent.WikiEditId,

            ArticleId = article.Id,
            PageId = article.WikiPageId,
            Title = article.Title,
            Wiki = article.Wiki,

            Extract = article.Extract,
            Categories = article.Categories?.Select(c => c.Name).ToList()
                         ?? new List<string>(),
            LinkedArticles = linkedArticles.ToList(),
            
            IsBot = editEvent.IsBot,
            DiffSize = editEvent.NewLength - editEvent.OldLength,

            Timestamp = editEvent.Timestamp,
            EnrichedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _producer.ProduceAsync(
                _topicsOptions.EnrichedEdits,
                pageId.ToString(),
                enrichedEvent,
                ct);

            _logger.LogDebug(
                "Published enriched event for {Title} to Kafka",
                enrichedEvent.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish enriched event for {Title}",
                enrichedEvent.Title);

            return Result<EnrichedEditEvent>.Failure(
                $"Failed to publish to Kafka: {ex.Message}");
        }
        return Result<EnrichedEditEvent>.Success(enrichedEvent);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
