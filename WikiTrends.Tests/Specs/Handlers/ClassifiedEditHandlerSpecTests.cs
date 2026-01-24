using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Analytics.Handlers;
using WikiTrends.Analytics.Services;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Tests.Specs.Handlers;

[Trait("Category", "Spec")]
public sealed class ClassifiedEditHandlerSpecTests
{
    [Fact]
    public async Task HandleAsync_CallsIngestionService_AndDoesNotThrow()
    {
        var ingestion = new Mock<IEventIngestionService>(MockBehavior.Strict);
        ingestion
            .Setup(s => s.IngestAsync(It.IsAny<ClassifiedEditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddScoped(_ => ingestion.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new ClassifiedEditHandler(scopeFactory, NullLogger<ClassifiedEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateClassifiedEdit(), CancellationToken.None));
        Assert.Null(ex);

        ingestion.Verify(s => s.IngestAsync(It.IsAny<ClassifiedEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_DoesNotThrow()
    {
        var ingestion = new Mock<IEventIngestionService>(MockBehavior.Strict);
        ingestion
            .Setup(s => s.IngestAsync(It.IsAny<ClassifiedEditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddScoped(_ => ingestion.Object);

        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var handler = new ClassifiedEditHandler(scopeFactory, NullLogger<ClassifiedEditHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(CreateClassifiedEdit(), CancellationToken.None));
        Assert.Null(ex);

        ingestion.Verify(s => s.IngestAsync(It.IsAny<ClassifiedEditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

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
