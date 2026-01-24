using WikiTrends.Aggregator.Cache;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Aggregator;

[Trait("Category", "Spec")]
public sealed class CacheKeysSpecTests
{
    [Theory]
    [InlineData(TrendPeriod.LastHour, "v3:trends:lasthour")]
    [InlineData(TrendPeriod.Last24Hours, "v3:trends:last24hours")]
    [InlineData(TrendPeriod.Last7Days, "v3:trends:last7days")]
    public void GetTrendsKey_UsesDeterministicVersionedFormat(TrendPeriod period, string expected)
    {
        var key = CacheKeys.GetTrendsKey(period);
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(1, TrendPeriod.LastHour, "v2:topic:1:lasthour")]
    [InlineData(42, TrendPeriod.Last24Hours, "v2:topic:42:last24hours")]
    public void GetTopicDetailsKey_UsesDeterministicVersionedFormat(int topicId, TrendPeriod period, string expected)
    {
        var key = CacheKeys.GetTopicDetailsKey(topicId, period);
        Assert.Equal(expected, key);
    }
}
