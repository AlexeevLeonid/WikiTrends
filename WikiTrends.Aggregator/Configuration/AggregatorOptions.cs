using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Aggregator.Configuration;

public sealed class AggregatorOptions
{
    public const string SectionName = "Aggregator";

    [Range(5, 3600)]
    public int TrendCacheRefreshSeconds { get; set; } = 60;
}
