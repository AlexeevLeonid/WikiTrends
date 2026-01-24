using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Aggregator.Models;

public sealed record CachedTrends
{
    public required TrendPeriod Period { get; init; }
    public required TrendsResponse Response { get; init; }
    public required DateTimeOffset CachedAt { get; init; }
}
