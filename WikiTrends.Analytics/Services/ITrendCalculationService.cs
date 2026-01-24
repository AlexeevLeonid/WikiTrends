namespace WikiTrends.Analytics.Services;

public interface ITrendCalculationService
{
    Task CalculateAndPublishAsync(CancellationToken ct = default);
}
