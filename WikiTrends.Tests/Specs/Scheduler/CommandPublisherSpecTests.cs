using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Producer;
using WikiTrends.Scheduler.Services;

namespace WikiTrends.Tests.Specs.Scheduler;

[Trait("Category", "Spec")]
public sealed class CommandPublisherSpecTests
{
    [Fact]
    public async Task PublishRecalculateBaselineAsync_WhenTopicIdIsNull_PublishesToConfiguredTopic_WithAllKey_AndDoesNotThrow()
    {
        var recalcProducer = new Mock<IKafkaProducer<string, RecalculateBaselineCommand>>(MockBehavior.Strict);
        var invalidateProducer = new Mock<IKafkaProducer<string, InvalidateCacheCommand>>(MockBehavior.Strict);

        recalcProducer
            .Setup(p => p.ProduceAsync(
                "cmd.recalc",
                "all",
                It.IsAny<RecalculateBaselineCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, RecalculateBaselineCommand>())
            .Verifiable();

        var publisher = new CommandPublisher(
            recalcProducer.Object,
            invalidateProducer.Object,
            Options.Create(new TopicsOptions { RecalculateBaselineCommands = "cmd.recalc" }),
            NullLogger<CommandPublisher>.Instance);

        var cmd = new RecalculateBaselineCommand { TopicId = null, RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => publisher.PublishRecalculateBaselineAsync(cmd, CancellationToken.None));
        Assert.Null(ex);

        recalcProducer.VerifyAll();
    }

    [Fact]
    public async Task PublishRecalculateBaselineAsync_WhenTopicIdProvided_PublishesToConfiguredTopic_WithTopicIdKey_AndDoesNotThrow()
    {
        var recalcProducer = new Mock<IKafkaProducer<string, RecalculateBaselineCommand>>(MockBehavior.Strict);
        var invalidateProducer = new Mock<IKafkaProducer<string, InvalidateCacheCommand>>(MockBehavior.Strict);

        recalcProducer
            .Setup(p => p.ProduceAsync(
                "cmd.recalc",
                "42",
                It.IsAny<RecalculateBaselineCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, RecalculateBaselineCommand>())
            .Verifiable();

        var publisher = new CommandPublisher(
            recalcProducer.Object,
            invalidateProducer.Object,
            Options.Create(new TopicsOptions { RecalculateBaselineCommands = "cmd.recalc" }),
            NullLogger<CommandPublisher>.Instance);

        var cmd = new RecalculateBaselineCommand { TopicId = 42, RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => publisher.PublishRecalculateBaselineAsync(cmd, CancellationToken.None));
        Assert.Null(ex);

        recalcProducer.VerifyAll();
    }

    [Fact]
    public async Task PublishInvalidateCacheAsync_PublishesToConfiguredTopic_WithCacheKey_AndDoesNotThrow()
    {
        var recalcProducer = new Mock<IKafkaProducer<string, RecalculateBaselineCommand>>(MockBehavior.Strict);
        var invalidateProducer = new Mock<IKafkaProducer<string, InvalidateCacheCommand>>(MockBehavior.Strict);

        invalidateProducer
            .Setup(p => p.ProduceAsync(
                "cmd.invalidate",
                "cache-key",
                It.IsAny<InvalidateCacheCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, InvalidateCacheCommand>())
            .Verifiable();

        var publisher = new CommandPublisher(
            recalcProducer.Object,
            invalidateProducer.Object,
            Options.Create(new TopicsOptions { InvalidateCacheCommands = "cmd.invalidate" }),
            NullLogger<CommandPublisher>.Instance);

        var cmd = new InvalidateCacheCommand { CacheKey = "cache-key", RequestedAt = DateTimeOffset.UtcNow };

        var ex = await Record.ExceptionAsync(() => publisher.PublishInvalidateCacheAsync(cmd, CancellationToken.None));
        Assert.Null(ex);

        invalidateProducer.VerifyAll();
    }
}
