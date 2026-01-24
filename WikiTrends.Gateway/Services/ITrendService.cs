using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Gateway.Services;

public interface ITrendService
{
    Task<Result<TrendsResponse>> GetTrendsAsync(GetTrendsRequest request, CancellationToken ct = default);

    Task<Result<ClusterResponse>> GetClustersAsync(TrendPeriod period, CancellationToken ct = default);
}
