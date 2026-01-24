using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Classifier.Handlers;
using WikiTrends.Classifier.Services;

namespace WikiTrends.Tests.Specs.Handlers;

[Trait("Category", "Spec")]
public sealed class EnrichedEditHandlerSpecTests
{
    [Fact]
    public async Task HandleAsync_CallsClassificationService_AndDoesNotThrow()
    {
        var classification = new Mock<IClassificationService>(MockBehavior.Strict);
        classification
            .Setup(s => s.ClassifyAsync(It.IsAny<EnrichedEditEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ClassifiedEditEvent>.Success(CreateClassifiedEdit()))
            .Verifiable();

        var services = new ServiceCollection();
        services.AddScoped(_ => classification.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new EnrichedEditHandler(scopeFactory, NullLogger<EnrichedEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateEnrichedEdit(), CancellationToken.None));
        Assert.Null(ex);

        classification.Verify(s => s.ClassifyAsync(It.IsAny<EnrichedEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_DoesNotThrow()
    {
        var classification = new Mock<IClassificationService>(MockBehavior.Strict);
        classification
            .Setup(s => s.ClassifyAsync(It.IsAny<EnrichedEditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddScoped(_ => classification.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new EnrichedEditHandler(scopeFactory, NullLogger<EnrichedEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateEnrichedEdit(), CancellationToken.None));
        Assert.Null(ex);

        classification.Verify(s => s.ClassifyAsync(It.IsAny<EnrichedEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceReturnsFailure_DoesNotThrow()
    {
        var classification = new Mock<IClassificationService>(MockBehavior.Strict);
        classification
            .Setup(s => s.ClassifyAsync(It.IsAny<EnrichedEditEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ClassifiedEditEvent>.Failure("fail"));

        var services = new ServiceCollection();
        services.AddScoped(_ => classification.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new EnrichedEditHandler(scopeFactory, NullLogger<EnrichedEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateEnrichedEdit(), CancellationToken.None));
        Assert.Null(ex);
    }

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

    private static ClassifiedEditEvent CreateClassifiedEdit() => new()
    {
        EventId = "evt-1",
        WikiEditId = 1,
        ArticleId = 100,
        Title = "T",
        Wiki = "enwiki",
        Topics = new List<TopicScore>
        {
            new()
            {
                TopicId = 1,
                TopicName = "Topic",
                TopicPath = "Root/Topic",
                Confidence = 0.5f
            }
        },
        Embedding = Array.Empty<float>(),
        Timestamp = DateTimeOffset.UtcNow,
        ClassifiedAt = DateTimeOffset.UtcNow
    };
}
