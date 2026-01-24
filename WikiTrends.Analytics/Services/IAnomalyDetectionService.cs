using WikiTrends.Analytics.Models;

namespace WikiTrends.Analytics.Services;

public interface IAnomalyDetectionService
{
    Task<AnomalyResult> DetectAsync(TrendData trend, BaselineData baseline, CancellationToken ct = default);
}
