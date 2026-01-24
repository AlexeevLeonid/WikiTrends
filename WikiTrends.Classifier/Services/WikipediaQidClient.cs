using System.Text.Json;
using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public sealed class WikipediaQidClient : IWikipediaQidClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikipediaQidClient> _logger;

    public WikipediaQidClient(HttpClient httpClient, ILogger<WikipediaQidClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<string>> GetWikidataIdAsync(string title, string lang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<string>.Failure("Title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(lang))
        {
            return Result<string>.Failure("Lang cannot be empty.");
        }

        try
        {
            var safeTitle = Uri.EscapeDataString(title);
            var endpoint = $"https://{lang}.wikipedia.org/w/api.php";
            var url = $"{endpoint}?action=query&prop=pageprops&ppprop=wikibase_item&redirects=1&titles={safeTitle}&format=json";

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"Wikipedia API error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("query", out var queryEl)
                || !queryEl.TryGetProperty("pages", out var pagesEl)
                || pagesEl.ValueKind != JsonValueKind.Object)
            {
                return Result<string>.Failure("Wikipedia API response missing pages.");
            }

            foreach (var pageProp in pagesEl.EnumerateObject())
            {
                var pageEl = pageProp.Value;

                if (pageEl.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (pageEl.TryGetProperty("missing", out _))
                {
                    return Result<string>.Failure("Wikipedia page not found.");
                }

                if (!pageEl.TryGetProperty("pageprops", out var pagepropsEl)
                    || pagepropsEl.ValueKind != JsonValueKind.Object)
                {
                    return Result<string>.Failure("Wikipedia pageprops not found.");
                }

                if (!pagepropsEl.TryGetProperty("wikibase_item", out var qidEl)
                    || qidEl.ValueKind != JsonValueKind.String)
                {
                    return Result<string>.Failure("Wikipedia page does not have wikibase_item.");
                }

                var qid = qidEl.GetString();
                if (string.IsNullOrWhiteSpace(qid))
                {
                    return Result<string>.Failure("Wikipedia returned empty wikibase_item.");
                }

                return Result<string>.Success(qid);
            }

            return Result<string>.Failure("Wikipedia API returned no pages.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Wikipedia API response.");
            return Result<string>.Failure("Failed to parse Wikipedia API response.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling Wikipedia API.");
            return Result<string>.Failure("Network error while calling Wikipedia API.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling Wikipedia API.");
            return Result<string>.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
