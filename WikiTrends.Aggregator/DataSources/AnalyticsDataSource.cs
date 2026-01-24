using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Threading;

namespace WikiTrends.Aggregator.DataSources;

public sealed class AnalyticsDataSource : IAnalyticsDataSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalyticsDataSource> _logger;

    private static long _unavailableUntilUnixMs;

    public AnalyticsDataSource(
        HttpClient httpClient,
        ILogger<AnalyticsDataSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<TrendsResponse>> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать URL Analytics сервиса для получения трендов
        // TODO: 2. Сериализовать request в query string
        // TODO: 3. Выполнить GET запрос
        // TODO: 4. Обработать non-success и вернуть Result.Failure
        // TODO: 5. Десериализовать TrendsResponse и вернуть Result.Success
        if (request == null)
        {
            return Result<TrendsResponse>.Failure("Request is null");
        }

        if (IsTemporarilyUnavailable())
        {
            return Result<TrendsResponse>.Failure("Analytics HTTP is temporarily unavailable");
        }

        try
        {
            var query = new Dictionary<string, string?>
            {
                ["period"] = request.Period.ToString(),
                ["limit"] = request.Limit.ToString(CultureInfo.InvariantCulture),
                ["minAnomalyScore"] = request.MinAnomalyScore.ToString(CultureInfo.InvariantCulture)
            };

            var url = QueryHelpers.AddQueryString("/api/trends", query);
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Analytics trends request failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return Result<TrendsResponse>.Failure($"Analytics returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var dto = await response.Content.ReadFromJsonAsync<TrendsResponse>(cancellationToken: ct);
            return dto == null
                ? Result<TrendsResponse>.Failure("Analytics returned empty response")
                : Result<TrendsResponse>.Success(dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (IsTransientNetworkFailure(ex))
        {
            MarkTemporarilyUnavailable(TimeSpan.FromSeconds(30));
            _logger.LogWarning("Analytics HTTP is unavailable: {Message}", ex.Message);
            return Result<TrendsResponse>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics trends request failed.");
            return Result<TrendsResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<TopicDetailResponse>> GetTopicDetailsAsync(int topicId, TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать URL Analytics сервиса для деталей темы
        // TODO: 2. Выполнить GET запрос
        // TODO: 3. Десериализовать TopicDetailResponse
        if (topicId <= 0)
        {
            return Result<TopicDetailResponse>.Failure("TopicId must be positive");
        }

        if (IsTemporarilyUnavailable())
        {
            return Result<TopicDetailResponse>.Failure("Analytics HTTP is temporarily unavailable");
        }

        try
        {
            var url = QueryHelpers.AddQueryString($"/api/topics/{topicId}", new Dictionary<string, string?>
            {
                ["period"] = period.ToString()
            });

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Analytics topic details request failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return Result<TopicDetailResponse>.Failure($"Analytics returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var dto = await response.Content.ReadFromJsonAsync<TopicDetailResponse>(cancellationToken: ct);
            return dto == null
                ? Result<TopicDetailResponse>.Failure("Analytics returned empty response")
                : Result<TopicDetailResponse>.Success(dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (IsTransientNetworkFailure(ex))
        {
            MarkTemporarilyUnavailable(TimeSpan.FromSeconds(30));
            _logger.LogWarning("Analytics HTTP is unavailable: {Message}", ex.Message);
            return Result<TopicDetailResponse>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics topic details request failed.");
            return Result<TopicDetailResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ClusterResponse>> GetClustersAsync(TrendPeriod period, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать URL Analytics сервиса для кластеров
        // TODO: 2. Выполнить GET запрос
        // TODO: 3. Десериализовать ClusterResponse
        try
        {
            if (IsTemporarilyUnavailable())
            {
                return Result<ClusterResponse>.Failure("Analytics HTTP is temporarily unavailable");
            }

            var url = QueryHelpers.AddQueryString("/api/trends/clusters", new Dictionary<string, string?>
            {
                ["period"] = period.ToString()
            });

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Analytics clusters request failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return Result<ClusterResponse>.Failure($"Analytics returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var dto = await response.Content.ReadFromJsonAsync<ClusterResponse>(cancellationToken: ct);
            return dto == null
                ? Result<ClusterResponse>.Failure("Analytics returned empty response")
                : Result<ClusterResponse>.Success(dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (IsTransientNetworkFailure(ex))
        {
            MarkTemporarilyUnavailable(TimeSpan.FromSeconds(30));
            _logger.LogWarning("Analytics HTTP is unavailable: {Message}", ex.Message);
            return Result<ClusterResponse>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics clusters request failed.");
            return Result<ClusterResponse>.Failure(ex.Message);
        }
    }

    private static bool IsTemporarilyUnavailable()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var untilMs = Interlocked.Read(ref _unavailableUntilUnixMs);
        return untilMs > nowMs;
    }

    private static void MarkTemporarilyUnavailable(TimeSpan cooldown)
    {
        var untilMs = DateTimeOffset.UtcNow.Add(cooldown).ToUnixTimeMilliseconds();
        Interlocked.Exchange(ref _unavailableUntilUnixMs, untilMs);
    }

    private static bool IsTransientNetworkFailure(HttpRequestException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException;
    }
}
