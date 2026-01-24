using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public interface IWikidataSparqlClient
{
    Task<Result<IReadOnlyList<WikidataSparqlNode>>> GetTopicHierarchyAsync(string qid, string lang, CancellationToken ct = default);
}

public sealed record WikidataSparqlNode
{
    public required string Qid { get; init; }
    public required string Label { get; init; }
    public required int Sitelinks { get; init; }
}
