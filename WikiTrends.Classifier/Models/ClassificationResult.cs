using WikiTrends.Contracts.Events;

namespace WikiTrends.Classifier.Models;

public sealed record ClassificationResult
{
    public required IReadOnlyList<TopicScore> Topics { get; init; }
    public required float[] Embedding { get; init; }
}
