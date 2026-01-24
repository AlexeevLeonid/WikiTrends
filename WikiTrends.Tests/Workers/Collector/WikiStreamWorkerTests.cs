using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Collector.Mapping;
using WikiTrends.Collector.Models;
using WikiTrends.Collector.Services;
using WikiTrends.Collector.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;
using WikiTrends.Tests.TestHelpers;

namespace WikiTrends.Tests.Workers.Collector;

public sealed class WikiStreamWorkerTests
{
    [Fact]
    public async Task WhenStreamProvidesMappedEvent_ProducerIsCalledWithRawEditsTopic()
    {
        var produced = new TaskCompletionSource<(string topic, string key)>(TaskCreationOptions.RunContinuationsAsynchronously);

        var producer = new Mock<IKafkaProducer<string, RawEditEvent>>(MockBehavior.Strict);
        producer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RawEditEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback((string topic, string key, RawEditEvent _, CancellationToken _) => produced.TrySetResult((topic, key)))
            .ReturnsAsync(new DeliveryResult<string, RawEditEvent>());

        var mapper = new Mock<IEditMapper>(MockBehavior.Strict);
        mapper
            .Setup(m => m.Map(It.IsAny<WikiRecentChange>()))
            .Returns(new RawEditEvent
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
            });

        var fakeStream = new FakeWikiStreamClient(new[]
        {
            new WikiRecentChange { PageId = 42, Wiki = "enwiki", Title = "T", Type = "edit" },
            null
        });

        var services = new ServiceCollection();
        services.AddSingleton<IWikiStreamClient>(fakeStream);
        services.AddSingleton(mapper.Object);
        var provider = services.BuildServiceProvider();

        var topics = Options.Create(new TopicsOptions
        {
            RawEdits = "wiki.raw-edits",
            EnrichedEdits = "wiki.enriched",
            ClassifiedEdits = "wiki.classified",
            TrendUpdates = "wiki.trend-updates",
            RecalculateBaselineCommands = "wiki.commands.recalculate-baseline",
            InvalidateCacheCommands = "wiki.commands.invalidate-cache"
        });

        var streamOptions = Options.Create(new WikiStreamOptions
        {
            ReconnectDelaySeconds = 1,
            MaxReconnectDelaySeconds = 1,
            HealthCheckIntervalSeconds = 1,
            AllowedTypes = ["edit"],
            AllowedNamespaces = [0],
            AllowedWikis = ["enwiki"],
            StreamUrl = "http://localhost"
        });

        var worker = new WikiStreamWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            producer.Object,
            topics,
            streamOptions,
            NullLogger<WikiStreamWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var result = await Task.WhenAny(produced.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(result == produced.Task, "Producer was not called in time");

        var (topic, key) = await produced.Task;
        Assert.Equal("wiki.raw-edits", topic);
        Assert.Equal("42", key);

        await worker.StopAsync(CancellationToken.None);

        producer.VerifyAll();
        mapper.VerifyAll();
    }
}
