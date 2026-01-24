using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.DataSources;
using WikiTrends.Aggregator.Services;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Aggregator;

[Trait("Category", "Spec")]
public sealed class AggregationServiceSpecTests
{
    [Fact]
    public async Task GetTrendsAsync_WhenCacheHit_ReturnsDeserializedResponse_AndDoesNotCallAnalytics()
    {
        var response = new TrendsResponse
        {
            Period = TrendPeriod.Last24Hours,
            Topics = Array.Empty<TopicTrendDto>(),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        cache
            .Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var analytics = new Mock<IAnalyticsDataSource>(MockBehavior.Strict);

        var service = new AggregationService(
            cache.Object,
            analytics.Object,
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        var result = await service.GetTrendsAsync(new GetTrendsRequest { Period = TrendPeriod.Last24Hours }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(TrendPeriod.Last24Hours, result.Value!.Period);

        analytics.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTrendsAsync_WhenCacheMiss_CallsAnalytics_AndWritesCache()
    {
        var response = new TrendsResponse
        {
            Period = TrendPeriod.Last24Hours,
            Topics = Array.Empty<TopicTrendDto>(),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        cache
            .Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        cache
            .Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<TimeSpan>(t => t > TimeSpan.Zero), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var analytics = new Mock<IAnalyticsDataSource>(MockBehavior.Strict);
        analytics
            .Setup(a => a.GetTrendsAsync(It.IsAny<GetTrendsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrendsResponse>.Success(response));

        var service = new AggregationService(
            cache.Object,
            analytics.Object,
            Mock.Of<IClassifierDataSource>(),
            Mock.Of<IEnricherDataSource>(),
            NullLogger<AggregationService>.Instance);

        var result = await service.GetTrendsAsync(new GetTrendsRequest { Period = TrendPeriod.Last24Hours }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        analytics.Verify(a => a.GetTrendsAsync(It.IsAny<GetTrendsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<TimeSpan>(t => t > TimeSpan.Zero), It.IsAny<CancellationToken>()), Times.Once);
    }
}
