namespace WikiTrends.Enricher.Models;

public sealed record WikipediaApiResponse
{
    public required long PageId { get; init; }
    public required string? Extract { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> LinkedArticles { get; init; }
}
