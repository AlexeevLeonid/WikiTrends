namespace WikiTrends.Classifier.Models;

public sealed record WikidataResponse
{
    public required WikidataEntity? Entity { get; init; }
    public required IReadOnlyList<string> Claims { get; init; }
}
