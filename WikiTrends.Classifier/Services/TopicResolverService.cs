using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WikiTrends.Classifier.Data.Entities;
using WikiTrends.Classifier.Data.Repositories;
using WikiTrends.Classifier.Configuration;
using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public sealed class TopicResolverService : ITopicResolverService
{
    private const int RootSitelinksThreshold = 50;

    private const string FallbackTopicName = "Uncategorized";
    private const float FallbackConfidence = 0.1f;

    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromHours(1);

    private readonly IWikidataMappingRepository _wikidataMappingRepository;
    private readonly IWikipediaQidClient _wikipediaQidClient;
    private readonly IWikidataSparqlClient _sparqlClient;
    private readonly IWikidataClient _wikidataClient;
    private readonly ITopicRepository _topicRepository;
    private readonly IArticleTopicRepository _articleTopicRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TopicResolverService> _logger;

    private readonly ClassifierOptions _options;

    public TopicResolverService(
        IWikidataMappingRepository wikidataMappingRepository,
        IWikipediaQidClient wikipediaQidClient,
        IWikidataSparqlClient sparqlClient,
        IWikidataClient wikidataClient,
        ITopicRepository topicRepository,
        IArticleTopicRepository articleTopicRepository,
        IMemoryCache memoryCache,
        IOptions<ClassifierOptions> options,
        ILogger<TopicResolverService> logger)
    {
        _wikidataMappingRepository = wikidataMappingRepository;
        _wikipediaQidClient = wikipediaQidClient;
        _sparqlClient = sparqlClient;
        _wikidataClient = wikidataClient;
        _topicRepository = topicRepository;
        _articleTopicRepository = articleTopicRepository;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<ArticleTopicEntity>> ResolveAndSaveTopicAsync(long articleId, string title, string lang, CancellationToken ct = default)
    {
        if (articleId <= 0)
        {
            return Result<ArticleTopicEntity>.Failure("ArticleId must be positive");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<ArticleTopicEntity>.Failure("Title cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(lang))
        {
            return Result<ArticleTopicEntity>.Failure("Lang cannot be empty");
        }

        var normalizedTitle = title.Trim();
        var normalizedLang = lang.Trim().ToLowerInvariant();
        var wikiCode = normalizedLang + "wiki";

        try
        {
            var existingArticleTopics = await _articleTopicRepository.GetByArticleIdAsync(articleId, ct);
            var existingTopic = existingArticleTopics
                .Where(x => x.Topic != null)
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();

            if (existingTopic?.Topic != null)
            {
                _logger.LogInformation(
                    "Resolved topic from article-topic cache for Article {ArticleId}. Topic={Topic} Confidence={Confidence}",
                    articleId,
                    existingTopic.Topic.Name,
                    existingTopic.Confidence);

                return Result<ArticleTopicEntity>.Success(existingTopic);
            }

            var mapping = await _wikidataMappingRepository.GetAsync(wikiCode, normalizedTitle, ct);
            var qid = mapping?.WikidataId;

            if (mapping != null && qid == null)
            {
                var age = DateTimeOffset.UtcNow - mapping.CachedAt;
                var negativeTtl = TimeSpan.FromHours(Math.Min(1, _options.WikidataCacheHours));
                if (age <= negativeTtl)
                {
                _logger.LogInformation(
                        "Resolved DEFAULT topic for Article {ArticleId}. Reason=NegativeCache Wiki={Wiki} Title={Title} AgeMinutes={AgeMinutes}",
                    articleId,
                    wikiCode,
                        normalizedTitle,
                        (int)age.TotalMinutes);

                return await ResolveFallbackAsync(articleId, ct);
                }

                _logger.LogInformation(
                    "Negative cache expired for Article {ArticleId}. Will re-check QID. Wiki={Wiki} Title={Title} AgeMinutes={AgeMinutes}",
                    articleId,
                    wikiCode,
                    normalizedTitle,
                    (int)age.TotalMinutes);
            }

            if (string.IsNullOrWhiteSpace(qid))
            {
                var qidResult = await _wikipediaQidClient.GetWikidataIdAsync(normalizedTitle, normalizedLang, ct);
                if (!qidResult.IsSuccess)
                {
                    var shouldNegativeCache = IsNegativeQidError(qidResult.Error);
                    if (shouldNegativeCache)
                    {
                        await _wikidataMappingRepository.UpsertAsync(wikiCode, normalizedTitle, null, ct);
                    }

                    _logger.LogWarning(
                        "Resolved DEFAULT topic for Article {ArticleId}. Reason=WikipediaQidError CachedNull={CachedNull} Wiki={Wiki} Title={Title} Error={Error}",
                        articleId,
                        shouldNegativeCache,
                        wikiCode,
                        normalizedTitle,
                        qidResult.Error);

                    var apiResolved = await TryResolveViaWikidataApiAsync(articleId, normalizedTitle, wikiCode, ct);
                    if (apiResolved != null)
                    {
                        return Result<ArticleTopicEntity>.Success(apiResolved);
                    }

                    return await ResolveFallbackAsync(articleId, ct);
                }

                qid = qidResult.Value;
                await _wikidataMappingRepository.UpsertAsync(wikiCode, normalizedTitle, qid, ct);
            }

            var cacheKey = GetSparqlTopicCacheKey(qid!, normalizedLang);
            if (_memoryCache.TryGetValue<CachedTopic>(cacheKey, out var cachedTopic) && cachedTopic != null)
            {
                var topicFromCache = await _topicRepository.UpsertAsync(new TopicEntity
                {
                    Name = cachedTopic.Name,
                    Path = cachedTopic.Path
                }, ct);

                var mappingFromCache = new ArticleTopicEntity
                {
                    ArticleId = articleId,
                    TopicId = topicFromCache.Id,
                    Confidence = cachedTopic.Confidence
                };

                await _articleTopicRepository.ReplaceAsync(articleId, new[] { mappingFromCache }, ct);
                mappingFromCache.Topic = topicFromCache;

                _logger.LogInformation(
                    "Resolved topic from cache for Article {ArticleId}. Lang={Lang} QID={Qid} Topic={Topic} Source={Source}",
                    articleId,
                    normalizedLang,
                    qid,
                    cachedTopic.Name,
                    cachedTopic.Source);

                return Result<ArticleTopicEntity>.Success(mappingFromCache);
            }

            var hierarchyResult = await _sparqlClient.GetTopicHierarchyAsync(qid!, normalizedLang, ct);
            if (!hierarchyResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Resolved DEFAULT topic for Article {ArticleId}. Reason=SparqlError Lang={Lang} QID={Qid} Error={Error}",
                    articleId,
                    normalizedLang,
                    qid,
                    hierarchyResult.Error);

                var apiResolved = await TryResolveViaWikidataApiAsync(articleId, normalizedTitle, wikiCode, ct);
                if (apiResolved != null)
                {
                    SetSparqlTopicCache(cacheKey, apiResolved.Topic!.Name, apiResolved.Topic.Path, apiResolved.Confidence, "WikidataApi");
                    return Result<ArticleTopicEntity>.Success(apiResolved);
                }

                return await ResolveFallbackAsync(articleId, ct);
            }

            var nodes = hierarchyResult.Value
                ?.Where(x => !string.IsNullOrWhiteSpace(x.Label))
                .ToList() ?? new List<WikidataSparqlNode>();

            if (nodes.Count == 0)
            {
                _logger.LogWarning(
                    "Resolved DEFAULT topic for Article {ArticleId}. Reason=EmptyHierarchy Lang={Lang} QID={Qid}",
                    articleId,
                    normalizedLang,
                    qid);

                var apiResolved = await TryResolveViaWikidataApiAsync(articleId, normalizedTitle, wikiCode, ct);
                if (apiResolved != null)
                {
                    SetSparqlTopicCache(cacheKey, apiResolved.Topic!.Name, apiResolved.Topic.Path, apiResolved.Confidence, "WikidataApi");
                    return Result<ArticleTopicEntity>.Success(apiResolved);
                }

                return await ResolveFallbackAsync(articleId, ct);
            }

            var rootIndex = nodes.FindIndex(x => x.Sitelinks >= RootSitelinksThreshold);
            if (rootIndex < 0)
            {
                rootIndex = nodes.Count - 1;
            }

            var root = nodes[rootIndex];
            var pathLabels = nodes
                .Take(rootIndex + 1)
                .Select(x => x.Label)
                .Reverse()
                .ToArray();

            var pathBreadcrumb = string.Join(" > ", pathLabels);

            var topic = await _topicRepository.UpsertAsync(new TopicEntity
            {
                Name = root.Label,
                Path = pathBreadcrumb
            }, ct);

            var mappingToSave = new ArticleTopicEntity
            {
                ArticleId = articleId,
                TopicId = topic.Id,
                Confidence = 1.0f
            };

            await _articleTopicRepository.ReplaceAsync(articleId, new[] { mappingToSave }, ct);

            mappingToSave.Topic = topic;

            _logger.LogInformation(
                "Resolved topic for Article {ArticleId}. Lang={Lang} QID={Qid} Root={Root} PathLen={PathLen}",
                articleId,
                normalizedLang,
                qid,
                root.Label,
                pathLabels.Length);

            SetSparqlTopicCache(cacheKey, topic.Name, topic.Path, mappingToSave.Confidence, "Sparql");

            return Result<ArticleTopicEntity>.Success(mappingToSave);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Topic resolution failed for Article {ArticleId}", articleId);
            return Result<ArticleTopicEntity>.Failure($"Topic resolution failed: {ex.Message}");
        }
    }

    private async Task<Result<ArticleTopicEntity>> ResolveFallbackAsync(long articleId, CancellationToken ct)
    {
        var topic = await _topicRepository.UpsertAsync(new TopicEntity
        {
            Name = FallbackTopicName,
            Path = FallbackTopicName
        }, ct);

        var mappingToSave = new ArticleTopicEntity
        {
            ArticleId = articleId,
            TopicId = topic.Id,
            Confidence = FallbackConfidence
        };

        await _articleTopicRepository.ReplaceAsync(articleId, new[] { mappingToSave }, ct);
        mappingToSave.Topic = topic;

        _logger.LogInformation(
            "Resolved fallback topic for Article {ArticleId}. Topic={Topic}",
            articleId,
            topic.Name);

        return Result<ArticleTopicEntity>.Success(mappingToSave);
    }

    private async Task<ArticleTopicEntity?> TryResolveViaWikidataApiAsync(long articleId, string title, string wikiCode, CancellationToken ct)
    {
        try
        {
            var entityResult = await _wikidataClient.GetEntityAsync(title, wikiCode, ct);
            if (!entityResult.IsSuccess || entityResult.Value?.Entity == null)
            {
                return null;
            }

            var instanceOf = entityResult.Value.Entity.InstanceOf;
            if (instanceOf == null || instanceOf.Count == 0)
            {
                return null;
            }

            var labelsResult = await _wikidataClient.GetLabelsAsync(instanceOf, wikiCode, ct);
            if (!labelsResult.IsSuccess || labelsResult.Value == null || labelsResult.Value.Count == 0)
            {
                return null;
            }

            var label = labelsResult.Value.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            var topic = await _topicRepository.UpsertAsync(new TopicEntity
            {
                Name = label,
                Path = label
            }, ct);

            var mappingToSave = new ArticleTopicEntity
            {
                ArticleId = articleId,
                TopicId = topic.Id,
                Confidence = 0.5f
            };

            await _articleTopicRepository.ReplaceAsync(articleId, new[] { mappingToSave }, ct);
            mappingToSave.Topic = topic;

            _logger.LogInformation(
                "Resolved topic via Wikidata API for Article {ArticleId}. Wiki={Wiki} Topic={Topic}",
                articleId,
                wikiCode,
                topic.Name);

            return mappingToSave;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve topic via Wikidata API for Article {ArticleId}", articleId);
            return null;
        }
    }

    private static bool IsNegativeQidError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("wikibase_item", StringComparison.OrdinalIgnoreCase)
               || error.Contains("pageprops", StringComparison.OrdinalIgnoreCase)
               || error.Contains("page not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSparqlTopicCacheKey(string qid, string lang) => $"sparql-topic:{qid}:{lang}";

    private void SetSparqlTopicCache(string cacheKey, string name, string path, float confidence, string source)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var ttlHours = Math.Clamp(_options.SparqlCacheHours, 1, 168);
        _memoryCache.Set(cacheKey, new CachedTopic
        {
            Name = name,
            Path = path,
            Confidence = confidence,
            Source = source
        }, TimeSpan.FromHours(ttlHours));
    }

    private sealed record CachedTopic
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required float Confidence { get; init; }
        public required string Source { get; init; }
    }
}
