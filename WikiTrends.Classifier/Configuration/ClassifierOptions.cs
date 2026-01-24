using System.ComponentModel.DataAnnotations;

namespace WikiTrends.Classifier.Configuration;

public sealed class ClassifierOptions
{
    public const string SectionName = "Classifier";

    [Range(1, 168)]
    public int WikidataCacheHours { get; set; } = 24;

    public bool SeedTopicsOnStartup { get; set; } = true;

    [Range(1, 64)]
    public int WorkerCount { get; set; } = 1;

    [Range(1, 60)]
    public int SparqlTimeoutSeconds { get; set; } = 8;

    [Range(10, 3600)]
    public int SparqlCircuitOpenSeconds { get; set; } = 300;

    [Range(1, 168)]
    public int SparqlCacheHours { get; set; } = 48;
}
