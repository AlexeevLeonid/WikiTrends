using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.Handlers;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Handlers;

[Trait("Category", "Spec")]
public sealed class CommandHandlerSpecTests
{
    [Fact]
    public async Task HandleAsync_InvalidateCache_RemovesCacheKey_AndDoesNotThrow()
    {
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        cache
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddScoped(_ => cache.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new CommandHandler(scopeFactory, NullLogger<CommandHandler>.Instance);

        var cmd = new InvalidateCacheCommand { CacheKey = "k", RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(cmd, CancellationToken.None));
        Assert.Null(ex);

        cache.Verify(c => c.RemoveAsync("k", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InvalidateCache_WhenCacheThrows_DoesNotThrow()
    {
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        cache
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddScoped(_ => cache.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new CommandHandler(scopeFactory, NullLogger<CommandHandler>.Instance);

        var cmd = new InvalidateCacheCommand { CacheKey = "k", RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(cmd, CancellationToken.None));
        Assert.Null(ex);

        cache.Verify(c => c.RemoveAsync("k", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RecalculateBaseline_DoesNotThrow()
    {
        var services = new ServiceCollection();

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new CommandHandler(scopeFactory, NullLogger<CommandHandler>.Instance);

        var cmd = new RecalculateBaselineCommand { TopicId = null, RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(cmd, CancellationToken.None));
        Assert.Null(ex);
    }
}
