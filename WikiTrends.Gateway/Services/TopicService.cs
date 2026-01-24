using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Gateway.Services;

public sealed class TopicService : ITopicService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceUrlsOptions _serviceUrls;
    private readonly ILogger<TopicService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TopicService(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceUrlsOptions> serviceUrls,
        ILogger<TopicService> logger)
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

    public async Task<Result<TopicDetailResponse>> GetTopicDetailsAsync(int topicId, TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Провалидировать topicId
        // TODO: 2. Получить базовый URL Aggregator из конфигурации
        // TODO: 3. Сформировать URL для GET /api/topics/{topicId}?period=...
        // TODO: 4. Выполнить HTTP GET, обработать статус
        // TODO: 5. Десериализовать TopicDetailResponse
        // TODO: 6. Вернуть Result.Success/Failure
        if (topicId <= 0)
        {
            return Result<TopicDetailResponse>.Failure("Invalid topicId");
        }

        try
        {
            var baseUrl = _serviceUrls.AggregatorBaseUrl.TrimEnd('/');
            var url = string.Create(CultureInfo.InvariantCulture, $"{baseUrl}/api/topics/{topicId}?period={period}");

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Aggregator topic details request failed. StatusCode={StatusCode}. TopicId={TopicId}. Body={Body}",
                    (int)response.StatusCode,
                    topicId,
                    body);
                return Result<TopicDetailResponse>.Failure($"Aggregator topic details request failed: {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<TopicDetailResponse>(json, _jsonOptions);
            return data is null
                ? Result<TopicDetailResponse>.Failure("Aggregator topic details response is empty")
                : Result<TopicDetailResponse>.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregator topic details request failed. TopicId={TopicId}", topicId);
            return Result<TopicDetailResponse>.Failure(ex.Message);
        }
    }
}
