using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Gateway.Services;

public sealed class TrendService : ITrendService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceUrlsOptions _serviceUrls;
    private readonly ILogger<TrendService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TrendService(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceUrlsOptions> serviceUrls,
        ILogger<TrendService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceUrls = serviceUrls.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<Result<TrendsResponse>> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default)
    {
        // TODO: 1. Получить базовый URL Aggregator из конфигурации
        // TODO: 2. Сформировать URL для GET /api/trends
        // TODO: 3. Выполнить HTTP GET, обработать статус
        // TODO: 4. Десериализовать TrendsResponse
        // TODO: 5. Вернуть Result.Success/Failure
        try
        {
            var baseUrl = _serviceUrls.AggregatorBaseUrl.TrimEnd('/');
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"{baseUrl}/api/trends?period={request.Period}&limit={request.Limit}&minAnomalyScore={request.MinAnomalyScore}");

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Aggregator trends request failed. StatusCode={StatusCode}. Body={Body}",
                    (int)response.StatusCode,
                    body);
                return Result<TrendsResponse>.Failure($"Aggregator trends request failed: {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<TrendsResponse>(json, _jsonOptions);
            return data is null
                ? Result<TrendsResponse>.Failure("Aggregator trends response is empty")
                : Result<TrendsResponse>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregator trends request failed.");
            return Result<TrendsResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ClusterResponse>> GetClustersAsync(TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Получить базовый URL Aggregator из конфигурации
        // TODO: 2. Сформировать URL для GET /api/trends/clusters?period=...
        // TODO: 3. Выполнить HTTP GET, обработать статус
        // TODO: 4. Десериализовать ClusterResponse
        // TODO: 5. Вернуть Result.Success/Failure
        try
        {
            var baseUrl = _serviceUrls.AggregatorBaseUrl.TrimEnd('/');
            var url = string.Create(CultureInfo.InvariantCulture, $"{baseUrl}/api/trends/clusters?period={period}");

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Aggregator clusters request failed. StatusCode={StatusCode}. Body={Body}",
                    (int)response.StatusCode,
                    body);
                return Result<ClusterResponse>.Failure($"Aggregator clusters request failed: {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<ClusterResponse>(json, _jsonOptions);
            return data is null
                ? Result<ClusterResponse>.Failure("Aggregator clusters response is empty")
                : Result<ClusterResponse>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregator clusters request failed.");
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }
}
