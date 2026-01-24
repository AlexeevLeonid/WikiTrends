using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Enricher.Configuration;

public sealed class EnricherOptions
{
    public const string SectionName = "Enricher";

    [Range(1, 168)]
    public int ArticleCacheHours { get; set; } = 4;

    [Range(1, 64)]
    public int WorkerCount { get; set; } = 1;
}
