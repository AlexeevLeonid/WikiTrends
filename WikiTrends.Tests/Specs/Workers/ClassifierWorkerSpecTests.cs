using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Classifier.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Tests.Specs.Workers;

[Trait("Category", "Spec")]
public sealed class ClassifierWorkerSpecTests
{
    [Fact]
    public async Task ExecuteAsync_StartsConsumerOnTopicsOptionsEnrichedEdits_AndStopsOnCancellation()
    {
        var started = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new Mock<IKafkaConsumer<string, EnrichedEditEvent>>(MockBehavior.Strict);
        consumer
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => started.TrySetResult(topic))
            .Returns(Task.CompletedTask);
        consumer
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var topics = Options.Create(new TopicsOptions { EnrichedEdits = "wiki.enriched" });

        var worker = new ClassifierWorker(consumer.Object, topics, NullLogger<ClassifierWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var execute = InvokeExecuteAsync(worker, cts.Token);

        var result = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.True(result == started.Task);
        Assert.Equal("wiki.enriched", await started.Task);

        cts.Cancel();

        await Task.WhenAny(execute, Task.Delay(TimeSpan.FromSeconds(1)));

        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(c => c.StartAsync("wiki.enriched", It.IsAny<CancellationToken>()), Times.Once);
        consumer.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Task InvokeExecuteAsync(BackgroundService worker, CancellationToken token)
    {
        var method = worker.GetType().GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task?)method!.Invoke(worker, new object[] { token });
        Assert.NotNull(task);

        return task!;
    }
}
