using Microsoft.Extensions.Logging.Abstractions;
using WikiTrends.Analytics.Models;
using WikiTrends.Analytics.Services;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Analytics;

[Trait("Category", "Spec")]
public sealed class AnomalyDetectionServiceSpecTests
{
    [Fact]
    public async Task DetectAsync_ComputesChangePercentAndScore_FromEditCountAndBaseline()
    {
        var svc = new AnomalyDetectionService(NullLogger<AnomalyDetectionService>.Instance);

        var trend = new TrendData { TopicId = 1, Period = TrendPeriod.Last24Hours, EditCount = 20, UniqueEditors = 1 };
        var baseline = new BaselineData { TopicId = 1, BaselineDaily = 10, CalculatedAt = DateTime.UtcNow };

        var result = await svc.DetectAsync(trend, baseline, CancellationToken.None);

        Assert.Equal(1f, result.ChangePercent);
        Assert.True(result.AnomalyScore > 3f);
    }

    [Fact]
    public async Task DetectAsync_WhenBaselineIsZero_ReturnsZeroes()
    {
        var svc = new AnomalyDetectionService(NullLogger<AnomalyDetectionService>.Instance);

        var trend = new TrendData { TopicId = 1, Period = TrendPeriod.Last24Hours, EditCount = 0, UniqueEditors = 0 };
        var baseline = new BaselineData { TopicId = 1, BaselineDaily = 0, CalculatedAt = DateTime.UtcNow };

        var result = await svc.DetectAsync(trend, baseline, CancellationToken.None);

        Assert.Equal(0f, result.ChangePercent);
        Assert.Equal(0f, result.AnomalyScore);
    }
}
