namespace WikiTrends.Analytics.Configuration;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public int TrendCalculationIntervalSeconds { get; set; } = 60;
    public int TrendCalculationParallelism { get; set; } = 10;

    public float MinAnomalyScoreToReport { get; set; } = 1.1f;
    public float MinChangePercentToReport { get; set; } = 0.2f;

    public int BaselineLookbackDays { get; set; } = 30;
    public int TopTopicsLimit { get; set; } = 50;
    public int TopArticlesPerTopicLimit { get; set; } = 20;
}
