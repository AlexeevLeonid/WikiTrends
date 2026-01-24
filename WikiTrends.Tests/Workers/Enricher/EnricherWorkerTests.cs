using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Workers;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Tests.Workers.Enricher;

public sealed class EnricherWorkerTests
{
    [Fact]
    public async Task StartAsync_SubscribesToRawEditsTopic_AndStopAsyncStopsConsumer()
    {
        var started = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new Mock<IKafkaConsumer<string, RawEditEvent>>(MockBehavior.Strict);
        consumer
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => started.TrySetResult(topic))
            .Returns(Task.CompletedTask);

        consumer
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var topics = Options.Create(new TopicsOptions
        {
            RawEdits = "wiki.raw-edits",
            EnrichedEdits = "wiki.enriched",
            ClassifiedEdits = "wiki.classified",
            TrendUpdates = "wiki.trend-updates",
            RecalculateBaselineCommands = "wiki.commands.recalculate-baseline",
            InvalidateCacheCommands = "wiki.commands.invalidate-cache"
        });

        var worker = new EnricherWorker(
            consumer.Object,
            topics,
            NullLogger<EnricherWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var result = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(result == started.Task, "Consumer was not started in time");

        Assert.Equal("wiki.raw-edits", await started.Task);

        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(c => c.StartAsync("wiki.raw-edits", It.IsAny<CancellationToken>()), Times.Once);
        consumer.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
