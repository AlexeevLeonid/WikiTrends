// ===== WikiTrends.Contracts/Api/Responses.cs =====
using WikiTrends.Contracts.Events;

namespace WikiTrends.Contracts.Api;

public sealed record TrendsResponse
{
    public required TrendPeriod Period { get; init; }
    public required IReadOnlyList<TopicTrendDto> Topics { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}

public sealed record TopicTrendDto
{
    public required int TopicId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required int EditCount { get; init; }
    public required float AnomalyScore { get; init; }
    public required TrendDirection Direction { get; init; }
    public required float ChangePercent { get; init; }
    public required IReadOnlyList<ArticleDto> TopArticles { get; init; }
}

public enum TrendDirection
{
    Rising,
    Stable,
    Falling
}

public sealed record ArticleDto
{
    public required long Id { get; init; }
    public required long WikiPageId { get; init; }
    public required string Title { get; init; }
    public required string Wiki { get; init; }
    public required string? Extract { get; init; }
    public required string WikiUrl { get; init; }
    public required int EditCount { get; init; }
    public required int UniqueEditors { get; init; }
    public required DateTimeOffset LastEditAt { get; init; }
}

public sealed record TopicDetailResponse
{
    public required int TopicId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required TopicStatsDto Stats { get; init; }
    public required IReadOnlyList<ArticleDto> Articles { get; init; }
    public required IReadOnlyList<RelatedTopicDto> RelatedTopics { get; init; }
}

public sealed record TopicStatsDto
{
    public required int EditCountLastHour { get; init; }
    public required int EditCountLast24Hours { get; init; }
    public required int EditCountLast7Days { get; init; }
    public required float BaselineDaily { get; init; }
    public required float AnomalyScore { get; init; }
}

public sealed record RelatedTopicDto
{
    public required int TopicId { get; init; }
    public required string Name { get; init; }
    public required float Similarity { get; init; }
}

public sealed record ClusterResponse
{
    public required IReadOnlyList<ClusterDto> Clusters { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}

public sealed record ClusterDto
{
    public required string ClusterId { get; init; }
    public required string Label { get; init; }  // автогенерированное название
    public required IReadOnlyList<int> TopicIds { get; init; }
    public required IReadOnlyList<ArticleDto> Articles { get; init; }
    public required float[] Centroid { get; init; }
    public required int TotalEdits { get; init; }
}

// ===== WikiTrends.Contracts/Api/Requests.cs =====
public sealed record GetTrendsRequest
{
    public TrendPeriod Period { get; init; } = TrendPeriod.Last24Hours;
    public int Limit { get; init; } = 20;
    public float MinAnomalyScore { get; init; } = 0;
}

public sealed record GetTopicArticlesRequest
{
    public int TopicId { get; init; }
    public TrendPeriod Period { get; init; } = TrendPeriod.Last24Hours;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

// ===== WikiTrends.Contracts/Api/SignalR.cs =====
public sealed record TrendNotification
{
    public required int TopicId { get; init; }
    public required string TopicName { get; init; }
    public required NotificationType Type { get; init; }
    public required float AnomalyScore { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public enum NotificationType
{
    NewSpike,
    TrendUpdate,
    NewCluster
}