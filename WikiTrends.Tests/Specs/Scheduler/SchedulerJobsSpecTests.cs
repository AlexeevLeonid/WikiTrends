using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Contracts.Events;
using WikiTrends.Scheduler.Jobs;
using WikiTrends.Scheduler.Services;

namespace WikiTrends.Tests.Specs.Scheduler;

[Trait("Category", "Spec")]
public sealed class SchedulerJobsSpecTests
{
    [Fact]
    public async Task BaselineRecalculationJob_ExecuteAsync_PublishesRecalculateCommand_WithTopicId_AndDoesNotThrow()
    {
        var publisher = new Mock<ICommandPublisher>(MockBehavior.Strict);
        publisher
            .Setup(p => p.PublishRecalculateBaselineAsync(
                It.Is<RecalculateBaselineCommand>(c => c.TopicId == 42 && c.RequestedAt != default),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var job = new BaselineRecalculationJob(publisher.Object, NullLogger<BaselineRecalculationJob>.Instance);

        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(42, CancellationToken.None));
        Assert.Null(ex);

        publisher.VerifyAll();
    }

    [Fact]
    public async Task BaselineRecalculationJob_ExecuteAsync_WhenTopicIdNull_PublishesRecalculateCommand_WithNullTopicId_AndDoesNotThrow()
    {
        var publisher = new Mock<ICommandPublisher>(MockBehavior.Strict);
        publisher
            .Setup(p => p.PublishRecalculateBaselineAsync(
                It.Is<RecalculateBaselineCommand>(c => c.TopicId == null && c.RequestedAt != default),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var job = new BaselineRecalculationJob(publisher.Object, NullLogger<BaselineRecalculationJob>.Instance);

        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(null, CancellationToken.None));
        Assert.Null(ex);

        publisher.VerifyAll();
    }

    [Fact]
    public async Task DataCleanupJob_ExecuteAsync_DoesNotThrow()
    {
        var job = new DataCleanupJob(NullLogger<DataCleanupJob>.Instance);

        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task SystemHealthCheckJob_ExecuteAsync_DoesNotThrow()
    {
        var job = new SystemHealthCheckJob(NullLogger<SystemHealthCheckJob>.Instance);

        var ex = await Record.ExceptionAsync(() => job.ExecuteAsync(CancellationToken.None));
        Assert.Null(ex);
    }
}
