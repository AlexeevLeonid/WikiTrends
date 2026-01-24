using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Aggregator.Handlers;
using WikiTrends.Aggregator.Services;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Handlers;

[Trait("Category", "Spec")]
public sealed class TrendUpdateHandlerSpecTests
{
    [Fact]
    public async Task HandleAsync_CallsAggregationService_AndDoesNotThrow()
    {
        var aggregation = new Mock<IAggregationService>(MockBehavior.Strict);
        aggregation
            .Setup(s => s.HandleTrendUpdateAsync(It.IsAny<TrendUpdateEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddScoped(_ => aggregation.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new TrendUpdateHandler(scopeFactory, NullLogger<TrendUpdateHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateTrendUpdate(), CancellationToken.None));
        Assert.Null(ex);

        aggregation.Verify(s => s.HandleTrendUpdateAsync(It.IsAny<TrendUpdateEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_DoesNotThrow()
    {
        var aggregation = new Mock<IAggregationService>(MockBehavior.Strict);
        aggregation
            .Setup(s => s.HandleTrendUpdateAsync(It.IsAny<TrendUpdateEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddScoped(_ => aggregation.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new TrendUpdateHandler(scopeFactory, NullLogger<TrendUpdateHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateTrendUpdate(), CancellationToken.None));
        Assert.Null(ex);

        aggregation.Verify(s => s.HandleTrendUpdateAsync(It.IsAny<TrendUpdateEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TrendUpdateEvent CreateTrendUpdate() => new()
    {
        EventId = "evt-1",
        Period = TrendPeriod.Last24Hours,
        Topics = Array.Empty<TopicTrend>(),
        CalculatedAt = DateTimeOffset.UtcNow
    };
}
