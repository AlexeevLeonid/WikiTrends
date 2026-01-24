namespace WikiTrends.Classifier.Models;

public sealed record WikidataEntity
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<string> InstanceOf { get; init; }
}
