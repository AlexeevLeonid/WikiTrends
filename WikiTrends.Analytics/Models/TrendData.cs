using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.Models;

public sealed record TrendData
{
    public required int TopicId { get; init; }
    public required TrendPeriod Period { get; init; }
    public required int EditCount { get; init; }
    public required int UniqueEditors { get; init; }
}
