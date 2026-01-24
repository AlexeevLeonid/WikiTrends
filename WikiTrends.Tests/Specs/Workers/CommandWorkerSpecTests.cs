using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Aggregator.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Tests.Specs.Workers;

[Trait("Category", "Spec")]
public sealed class CommandWorkerSpecTests
{
    [Fact]
    public async Task ExecuteAsync_StartsBothConsumersOnCommandTopics_AndStopsOnCancellation()
    {
        var startedRecalc = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedInvalidate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var recalc = new Mock<IKafkaConsumer<string, RecalculateBaselineCommand>>(MockBehavior.Strict);
        recalc
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => startedRecalc.TrySetResult(topic))
            .Returns(Task.CompletedTask);
        recalc
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var invalidate = new Mock<IKafkaConsumer<string, InvalidateCacheCommand>>(MockBehavior.Strict);
        invalidate
            .Setup(c => c.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string topic, CancellationToken _) => startedInvalidate.TrySetResult(topic))
            .Returns(Task.CompletedTask);
        invalidate
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var topics = Options.Create(new TopicsOptions
        {
            RecalculateBaselineCommands = "wiki.commands.recalculate-baseline",
            InvalidateCacheCommands = "wiki.commands.invalidate-cache"
        });

        var worker = new CommandWorker(recalc.Object, invalidate.Object, topics, NullLogger<CommandWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var execute = InvokeExecuteAsync(worker, cts.Token);

        var r1 = await Task.WhenAny(startedRecalc.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        var r2 = await Task.WhenAny(startedInvalidate.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.True(r1 == startedRecalc.Task);
        Assert.True(r2 == startedInvalidate.Task);

        Assert.Equal("wiki.commands.recalculate-baseline", await startedRecalc.Task);
        Assert.Equal("wiki.commands.invalidate-cache", await startedInvalidate.Task);

        cts.Cancel();

        await Task.WhenAny(execute, Task.Delay(TimeSpan.FromSeconds(1)));

        await worker.StopAsync(CancellationToken.None);

        recalc.Verify(c => c.StartAsync("wiki.commands.recalculate-baseline", It.IsAny<CancellationToken>()), Times.Once);
        invalidate.Verify(c => c.StartAsync("wiki.commands.invalidate-cache", It.IsAny<CancellationToken>()), Times.Once);
        recalc.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        invalidate.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
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
