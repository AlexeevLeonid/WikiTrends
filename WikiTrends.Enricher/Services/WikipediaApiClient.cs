using System.Text.Json;
using System.Text.Json.Serialization;
using WikiTrends.Contracts.Common;
using WikiTrends.Enricher.Models;

namespace WikiTrends.Enricher.Services;

public sealed class WikipediaApiClient : IWikipediaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikipediaApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WikipediaApiClient(
        HttpClient httpClient,
        ILogger<WikipediaApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Result<WikipediaApiResponse>> GetPageDataAsync(
        string title,
        string wiki,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<WikipediaApiResponse>.Failure("Title is required");
        }

        if (string.IsNullOrWhiteSpace(wiki))
        {
            return Result<WikipediaApiResponse>.Failure("Wiki is required");
        }


        var lang = ExtractLanguageCode(wiki);

        var baseUrl = $"https://{lang}.wikipedia.org/w/api.php";

        var queryParams = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["format"] = "json",
            ["prop"] = "extracts|categories|links",
            ["titles"] = title,
            ["exintro"] = "true",        // Только введение статьи
            ["explaintext"] = "true",    // Plain text без HTML
            ["cllimit"] = "50",          // Лимит категорий
            ["pllimit"] = "50",          // Лимит ссылок
            ["clshow"] = "!hidden"       // Исключить скрытые категории
        };

        var queryString = string.Join("&",
            queryParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var requestUrl = $"{baseUrl}?{queryString}";

        _logger.LogDebug("Requesting Wikipedia API: {Url}", requestUrl);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Wikipedia failed for title: {Title}", title);
            return Result<WikipediaApiResponse>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Wikipedia request timed out for title: {Title}", title);
            return Result<WikipediaApiResponse>.Failure("Request timed out");
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            _logger.LogWarning(
                "Wikipedia API returned {StatusCode} for title: {Title}",
                statusCode, title);

            return Result<WikipediaApiResponse>.Failure(
                $"Wikipedia API returned status {(int)statusCode}: {statusCode}");
        }

        WikiApiRawResponse? rawResponse;
        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            rawResponse = JsonSerializer.Deserialize<WikiApiRawResponse>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Wikipedia response for title: {Title}", title);
            return Result<WikipediaApiResponse>.Failure($"Failed to parse response: {ex.Message}");
        }

        if (rawResponse?.Query?.Pages == null || !rawResponse.Query.Pages.Any())
        {
            _logger.LogWarning("Wikipedia API returned empty pages for title: {Title}", title);
            return Result<WikipediaApiResponse>.Failure("No pages in response");
        }

        // Wikipedia возвращает словарь с одним элементом (ключ = pageId)
        var pageData = rawResponse.Query.Pages.Values.First();

        if (pageData.IsMissing)
        {
            _logger.LogDebug("Page not found: {Title} in {Wiki}", title, wiki);
            return Result<WikipediaApiResponse>.Failure($"Page not found: {title}");
        }

        var result = new WikipediaApiResponse
        {
            PageId = pageData.PageId ?? 0,
            Extract = pageData.Extract?.Trim(),

            Categories = pageData.Categories?
                .Where(c => !string.IsNullOrEmpty(c.Title))
                .Select(c => CleanCategoryName(c.Title!))
                .ToList()
                ?? new List<string>(),

            LinkedArticles = pageData.Links?
                .Where(l => !string.IsNullOrEmpty(l.Title))
                .Select(l => l.Title!)
                .ToList()
                ?? new List<string>()
        };

        _logger.LogDebug(
            "Successfully fetched page data for {Title}: Extract={ExtractLength} chars, " +
            "Categories={CategoriesCount}, Links={LinksCount}",
            title,
            result.Extract?.Length ?? 0,
            result.Categories.Count,
            result.LinkedArticles.Count);

        return Result<WikipediaApiResponse>.Success(result);
    }

    /// <summary>
    /// Преобразует "enwiki" - "en", "ruwiki" - "ru"
    /// </summary>
    private static string ExtractLanguageCode(string wiki)
    {
        if (wiki.EndsWith("wiki", StringComparison.OrdinalIgnoreCase))
        {
            return wiki[..^4];
        }
        return wiki;
    }

    /// <summary>
    /// Убирает префикс "Category:" из названия категории
    /// </summary>
    private static string CleanCategoryName(string categoryTitle)
    {
        var prefixes = new[] { "Category:", "Категория:", "Kategorie:", "Catégorie:" };
        foreach (var prefix in prefixes)
        {
            if (categoryTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return categoryTitle[prefix.Length..].Trim();
            }
        }

        return categoryTitle.Trim();
    }

    public sealed class WikiApiRawResponse
    {
        [JsonPropertyName("query")]
        public WikiQueryResult? Query { get; set; }
    }

    public sealed class WikiQueryResult
    {
        [JsonPropertyName("pages")]
        public Dictionary<string, WikiPageData>? Pages { get; set; }
    }

    public sealed class WikiPageData
    {
        [JsonPropertyName("pageid")]
        public long? PageId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [JsonPropertyName("categories")]
        public List<WikiCategoryItem>? Categories { get; set; }

        [JsonPropertyName("links")]
        public List<WikiLinkItem>? Links { get; set; }

        [JsonPropertyName("missing")]
        public object? Missing { get; set; }

        public bool IsMissing => Missing != null;
    }

    public sealed class WikiCategoryItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    public sealed class WikiLinkItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
