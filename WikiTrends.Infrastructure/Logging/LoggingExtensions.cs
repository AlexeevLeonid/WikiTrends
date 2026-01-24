using Microsoft.Extensions.Logging;

namespace WikiTrends.Infrastructure.Logging;

/// <summary>
/// High-performance logging с source generators.
/// Используй [LoggerMessage] атрибут для compile-time генерации.
/// </summary>
public static partial class LoggingExtensions
{
    // Kafka Producer logs
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Message delivered to {Topic}[{Partition}]@{Offset}")]
    public static partial void LogMessageDelivered(
        this ILogger logger,
        string topic,
        int partition,
        long offset);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Failed to deliver message to {Topic}")]
    public static partial void LogDeliveryFailed(
        this ILogger logger,
        string topic,
        Exception exception);

    // Kafka Consumer logs
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Starting Kafka consumer for topic {Topic}")]
    public static partial void LogConsumerStarting(
        this ILogger logger,
        string topic);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Received message from {Topic}[{Partition}]@{Offset}")]
    public static partial void LogMessageReceived(
        this ILogger logger,
        string topic,
        int partition,
        long offset);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Error consuming from {Topic}")]
    public static partial void LogConsumeError(
        this ILogger logger,
        string topic,
        Exception exception);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Kafka consumer stopped for topic {Topic}")]
    public static partial void LogConsumerStopped(
        this ILogger logger,
        string topic);

    // Database logs
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Database migration completed for {ContextName}")]
    public static partial void LogMigrationCompleted(
        this ILogger logger,
        string contextName);
}