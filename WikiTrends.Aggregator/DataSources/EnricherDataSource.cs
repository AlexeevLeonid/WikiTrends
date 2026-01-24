using WikiTrends.Contracts.Common;
using System.Net.Http.Json;

namespace WikiTrends.Aggregator.DataSources;

public sealed class EnricherDataSource : IEnricherDataSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EnricherDataSource> _logger;

    public EnricherDataSource(
        HttpClient httpClient,
        ILogger<EnricherDataSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<object>> GetArticleAsync(long articleId, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать URL Enricher сервиса для статьи articleId
        // TODO: 2. Выполнить GET запрос
        // TODO: 3. Вернуть Result.Success(объект) или Result.Failure
        if (articleId <= 0)
        {
            return Result<object>.Failure("ArticleId must be positive");
        }

        try
        {
            using var response = await _httpClient.GetAsync($"/api/articles/{articleId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Enricher article request failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return Result<object>.Failure($"Enricher returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var data = await response.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
            return data == null
                ? Result<object>.Failure("Enricher returned empty response")
                : Result<object>.Success(data);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enricher article request failed.");
            return Result<object>.Failure(ex.Message);
        }
    }
}
