using System.Net.Http.Json;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Frontend.Services;

public sealed class GatewayApiClient
{
    private readonly HttpClient _http;

    public GatewayApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<HealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<HealthResponse>("/api/health", cancellationToken: ct);
    }

    public async Task<TrendsResponse?> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default)
    {
        var url = $"/api/trends?period={request.Period}&limit={request.Limit}&minAnomalyScore={request.MinAnomalyScore}";
        return await _http.GetFromJsonAsync<TrendsResponse>(url, cancellationToken: ct);
    }

    public async Task<TopicDetailResponse?> GetTopicAsync(int topicId, TrendPeriod period, CancellationToken ct = default)
    {
        var url = $"/api/topics/{topicId}?period={period}";
        return await _http.GetFromJsonAsync<TopicDetailResponse>(url, cancellationToken: ct);
    }

    public async Task<ClusterResponse?> GetClustersAsync(TrendPeriod period, CancellationToken ct = default)
    {
        var url = $"/api/trends/clusters?period={period}";
        return await _http.GetFromJsonAsync<ClusterResponse>(url, cancellationToken: ct);
    }
}

public sealed record HealthResponse
{
    public required string Status { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
