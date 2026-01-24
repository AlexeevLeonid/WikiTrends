using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Handlers;
using WikiTrends.Enricher.Services;

namespace WikiTrends.Tests.Specs.Handlers;

[Trait("Category", "Spec")]
public sealed class RawEditHandlerSpecTests
{
    [Fact]
    public async Task HandleAsync_CallsEnrichmentService_AndDoesNotThrow()
    {
        var enrichment = new Mock<IEnrichmentService>(MockBehavior.Strict);
        enrichment
            .Setup(s => s.EnrichAsync(It.IsAny<RawEditEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EnrichedEditEvent>.Success(CreateEnrichedEdit()))
            .Verifiable();

        var services = new ServiceCollection();
        services.AddScoped(_ => enrichment.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new RawEditHandler(scopeFactory, NullLogger<RawEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateRawEdit(), CancellationToken.None));
        Assert.Null(ex);

        enrichment.Verify(s => s.EnrichAsync(It.IsAny<RawEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_DoesNotThrow()
    {
        var enrichment = new Mock<IEnrichmentService>(MockBehavior.Strict);
        enrichment
            .Setup(s => s.EnrichAsync(It.IsAny<RawEditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddScoped(_ => enrichment.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new RawEditHandler(scopeFactory, NullLogger<RawEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateRawEdit(), CancellationToken.None));
        Assert.Null(ex);

        enrichment.Verify(s => s.EnrichAsync(It.IsAny<RawEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceReturnsFailure_DoesNotThrow()
    {
        var enrichment = new Mock<IEnrichmentService>(MockBehavior.Strict);
        enrichment
            .Setup(s => s.EnrichAsync(It.IsAny<RawEditEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EnrichedEditEvent>.Failure("fail"));

        var services = new ServiceCollection();
        services.AddScoped(_ => enrichment.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new RawEditHandler(scopeFactory, NullLogger<RawEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateRawEdit(), CancellationToken.None));
        Assert.Null(ex);
    }

    private static RawEditEvent CreateRawEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        PageId = 42,
        Title = "T",
        Wiki = "enwiki",
        User = "u",
        IsBot = false,
        IsMinor = false,
        IsNew = false,
        OldLength = 1,
        NewLength = 2,
        Timestamp = DateTimeOffset.UtcNow,
        CollectedAt = DateTimeOffset.UtcNow
    };

    private static EnrichedEditEvent CreateEnrichedEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        ArticleId = 100,
        PageId = 42,
        Title = "T",
        Wiki = "enwiki",
        Extract = "x",
        Categories = new List<string> { "C" },
        LinkedArticles = new List<string>(),
        IsBot = false,
        DiffSize = 1,
        Timestamp = DateTimeOffset.UtcNow,
        EnrichedAt = DateTimeOffset.UtcNow
    };
}
