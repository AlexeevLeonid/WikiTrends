namespace WikiTrends.Analytics.Models;

public sealed record TopicStats
{
    public required int TopicId { get; init; }
    public required int EditCount { get; init; }
    public required int UniqueEditors { get; init; }
    public required DateTimeOffset LastEditAt { get; init; }
}
