using System.Reflection;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Workers;
using WikiTrends.Aggregator.Configuration;
using WikiTrends.Aggregator.Workers;
using WikiTrends.Classifier.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Gateway.Hubs;
using WikiTrends.Gateway.Workers;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Tests.Workers.StubWorkers;

[Trait("Category", "Legacy")]
public sealed class NotImplementedWorkerTests
{
    [Fact]
    public async Task ClassifierWorker_StartsConsumerOnEnrichedEditsTopic_AndStopsConsumer()
    {
        var started = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new Mock<IKafkaConsumer<string, EnrichedEditEvent>>();
        consumer
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => started.TrySetResult(topic))
            .Returns(Task.CompletedTask);

        consumer
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new ClassifierWorker(
            consumer.Object,
            Options.Create(new WikiTrends.Infrastructure.Configuration.TopicsOptions
            {
                EnrichedEdits = "wiki.enriched"
            }),
            NullLogger<ClassifierWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var result = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(result == started.Task, "Consumer was not started in time");
        Assert.Equal("wiki.enriched", await started.Task);

        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(c => c.StartAsync("wiki.enriched", It.IsAny<CancellationToken>()), Times.Once);
        consumer.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Analytics_EventConsumerWorker_StartsConsumerOnClassifiedEditsTopic_AndStopsConsumer()
    {
        var started = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new Mock<IKafkaConsumer<string, ClassifiedEditEvent>>();
        consumer
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => started.TrySetResult(topic))
            .Returns(Task.CompletedTask);

        consumer
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new EventConsumerWorker(
            consumer.Object,
            Options.Create(new WikiTrends.Infrastructure.Configuration.TopicsOptions
            {
                ClassifiedEdits = "wiki.classified"
            }),
            NullLogger<EventConsumerWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var result = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(result == started.Task, "Consumer was not started in time");
        Assert.Equal("wiki.classified", await started.Task);

        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(c => c.StartAsync("wiki.classified", It.IsAny<CancellationToken>()), Times.Once);
        consumer.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Analytics_TrendCalculationWorker_ExecuteAsync_UsesOptionsInterval_AndInvokesService()
    {
        var options = Options.Create(new AnalyticsOptions { TrendCalculationIntervalSeconds = 60 });
        var worker = new TrendCalculationWorker(
            Mock.Of<IServiceScopeFactory>(),
            options,
            NullLogger<TrendCalculationWorker>.Instance);

        await AssertThrowsFromExecuteAsync(worker);
    }

    [Fact]
    public async Task Aggregator_CommandWorker_StartsBothConsumersOnCommandTopics_AndStopsBoth()
    {
        var startedRecalc = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedInvalidate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var recalc = new Mock<IKafkaConsumer<string, RecalculateBaselineCommand>>();
        recalc
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => startedRecalc.TrySetResult(topic))
            .Returns(Task.CompletedTask);
        recalc
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var invalidate = new Mock<IKafkaConsumer<string, InvalidateCacheCommand>>();
        invalidate
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => startedInvalidate.TrySetResult(topic))
            .Returns(Task.CompletedTask);
        invalidate
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new CommandWorker(
            recalc.Object,
            invalidate.Object,
            Options.Create(new WikiTrends.Infrastructure.Configuration.TopicsOptions()),
            NullLogger<CommandWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var r1 = await Task.WhenAny(startedRecalc.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        var r2 = await Task.WhenAny(startedInvalidate.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(r1 == startedRecalc.Task, "Recalculate consumer was not started in time");
        Assert.True(r2 == startedInvalidate.Task, "Invalidate consumer was not started in time");

        Assert.Equal("wiki.commands.recalculate-baseline", await startedRecalc.Task);
        Assert.Equal("wiki.commands.invalidate-cache", await startedInvalidate.Task);

        await worker.StopAsync(CancellationToken.None);

        recalc.Verify(c => c.StartAsync("wiki.commands.recalculate-baseline", It.IsAny<CancellationToken>()), Times.Once);
        invalidate.Verify(c => c.StartAsync("wiki.commands.invalidate-cache", It.IsAny<CancellationToken>()), Times.Once);
        recalc.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        invalidate.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Aggregator_TrendCacheWorker_ExecuteAsync_PeriodicallyRefreshesCache()
    {
        var worker = new TrendCacheWorker(
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new AggregatorOptions()),
            NullLogger<TrendCacheWorker>.Instance);
        await AssertThrowsFromExecuteAsync(worker);
    }

    [Fact]
    public async Task Gateway_TrendBroadcastWorker_ExecuteAsync_ThrowsNotImplemented()
    {
        var hub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<TrendHub, ITrendHubClient>>();
        var httpClientFactory = Mock.Of<IHttpClientFactory>();
        var serviceUrls = Options.Create(new ServiceUrlsOptions());
        var worker = new TrendBroadcastWorker(hub.Object, httpClientFactory, serviceUrls, NullLogger<TrendBroadcastWorker>.Instance);
        await AssertThrowsFromExecuteAsync(worker);
    }

    private static async Task AssertThrowsFromExecuteAsync(BackgroundService worker)
    {
        var method = worker.GetType().GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task?)method!.Invoke(worker, new object[] { CancellationToken.None });
        Assert.NotNull(task);

        await Assert.ThrowsAsync<NotImplementedException>(() => task!);
    }
}
