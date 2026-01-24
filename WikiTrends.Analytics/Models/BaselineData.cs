namespace WikiTrends.Analytics.Models;

public sealed record BaselineData
{
    public required int TopicId { get; init; }
    public required float BaselineDaily { get; init; }
    public required DateTime CalculatedAt { get; init; }
}
