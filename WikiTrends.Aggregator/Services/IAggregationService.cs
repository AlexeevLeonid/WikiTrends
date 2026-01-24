using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Aggregator.Services;

public interface IAggregationService
{
    Task<Result<TrendsResponse>> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default);

    Task<Result<TopicDetailResponse>> GetTopicDetailsAsync(int topicId, TrendPeriod period, CancellationToken ct = default);

    Task<Result<ClusterResponse>> GetClustersAsync(TrendPeriod period, CancellationToken ct = default);

    Task HandleTrendUpdateAsync(TrendUpdateEvent message, CancellationToken ct = default);
}
