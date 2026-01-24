namespace WikiTrends.Scheduler.Jobs;

public sealed class DataCleanupJob
{
    private readonly ILogger<DataCleanupJob> _logger;

    public DataCleanupJob(ILogger<DataCleanupJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: 1. Определить политики очистки (retention) для ClickHouse/Redis/Postgres
        // TODO: 2. Запустить очистку по источникам (возможно, через HTTP/SQL/CLI)
        // TODO: 3. Логировать прогресс
        // TODO: 4. Обработать ошибки (логировать и продолжать)
        await Task.Yield();
        _logger.LogInformation("Data cleanup job executed (no-op).");
    }
}
