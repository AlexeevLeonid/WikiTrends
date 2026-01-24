namespace WikiTrends.Analytics.Models;

public sealed record AnomalyResult
{
    public required float AnomalyScore { get; init; }
    public required float ChangePercent { get; init; }
}
