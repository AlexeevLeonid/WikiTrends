namespace WikiTrends.Scheduler.Jobs;

public sealed class SystemHealthCheckJob
{
    private readonly ILogger<SystemHealthCheckJob> _logger;

    public SystemHealthCheckJob(ILogger<SystemHealthCheckJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: 1. Проверить доступность ключевых зависимостей (Kafka, Redis, Postgres, ClickHouse)
        // TODO: 2. Залогировать состояние и latency
        // TODO: 3. При проблемах — отправить алерт (в будущем)
        var startedAt = DateTimeOffset.UtcNow;

        await Task.Yield();

        var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        _logger.LogInformation("System health check completed. Status=OK. ElapsedMs={ElapsedMs}", elapsedMs);
    }
}
