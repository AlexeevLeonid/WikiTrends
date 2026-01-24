namespace WikiTrends.Contracts.Events;

public sealed record RawEditEvent
{
    public required string EventId { get; init; }
    public required long WikiEditId { get; init; }
    public required long PageId { get; init; }
    public required string Title { get; init; }
    public required string Wiki { get; init; }
    public required string User { get; init; }
    public required bool IsBot { get; init; }
    public required bool IsMinor { get; init; }
    public required bool IsNew { get; init; }
    public required int OldLength { get; init; }
    public required int NewLength { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
}

public sealed record EnrichedEditEvent
{
    public required string EventId { get; init; }
    public required long WikiEditId { get; init; }
    public required long ArticleId { get; init; }  // internal ID
    public required long PageId { get; init; }     // Wikipedia page ID
    public required string Title { get; init; }
    public required string Wiki { get; init; }
    public required string? Extract { get; init; }  // краткое описание
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> LinkedArticles { get; init; }
    public required int DiffSize { get; init; }
    public required bool IsBot { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset EnrichedAt { get; init; }
}

public sealed record ClassifiedEditEvent
{
    public required string EventId { get; init; }
    public required long WikiEditId { get; init; }
    public required long ArticleId { get; init; }
    public required string Title { get; init; }
    public required string Wiki { get; init; }
    public required IReadOnlyList<TopicScore> Topics { get; init; }
    public required float[] Embedding { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset ClassifiedAt { get; init; }
}

public sealed record TopicScore
{
    public required int TopicId { get; init; }
    public required string TopicName { get; init; }
    public required string TopicPath { get; init; }  // "Science/Biology/Genetics"
    public required float Confidence { get; init; }
}

public sealed record TrendUpdateEvent
{
    public required string EventId { get; init; }
    public required TrendPeriod Period { get; init; }
    public required IReadOnlyList<TopicTrend> Topics { get; init; }
    public required DateTimeOffset CalculatedAt { get; init; }
}

public enum TrendPeriod
{
    LastHour,
    Last24Hours,
    Last7Days
}

public sealed record TopicTrend
{
    public required int TopicId { get; init; }
    public required string TopicName { get; init; }
    public required int EditCount { get; init; }
    public required float AnomalyScore { get; init; }  // z-score
    public required float ChangePercent { get; init; }  // vs baseline
    public required IReadOnlyList<ArticleTrend> TopArticles { get; init; }
}

public sealed record ArticleTrend
{
    public required long ArticleId { get; init; }
    public required string Title { get; init; }
    public required int EditCount { get; init; }
    public required int UniqueEditors { get; init; }
}

public sealed record RecalculateBaselineCommand
{
    public int? TopicId { get; init; }  // null = все темы
    public required DateTimeOffset RequestedAt { get; init; }
}

public sealed record InvalidateCacheCommand
{
    public required string CacheKey { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
}