using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Infrastructure.Configuration;

public sealed class TopicsOptions
{
    public const string SectionName = "Topics";

    [Required]
    public string RawEdits { get; set; } = "wiki.raw-edits";

    [Required]
    public string EnrichedEdits { get; set; } = "wiki.enriched";

    [Required]
    public string ClassifiedEdits { get; set; } = "wiki.classified";

    [Required]
    public string TrendUpdates { get; set; } = "wiki.trend-updates";

    [Required]
    public string RecalculateBaselineCommands { get; set; } = "wiki.commands.recalculate-baseline";

    [Required]
    public string InvalidateCacheCommands { get; set; } = "wiki.commands.invalidate-cache";
}
