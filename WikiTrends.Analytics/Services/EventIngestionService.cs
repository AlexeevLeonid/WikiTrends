using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.Services;

public sealed class EventIngestionService : IEventIngestionService
{
    private readonly IClickHouseClient _clickHouseClient;
    private readonly ILogger<EventIngestionService> _logger;
    private static volatile bool _isSchemaInitialized = false;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    public EventIngestionService(
        IClickHouseClient clickHouseClient,
        ILogger<EventIngestionService> logger)
    {
        _clickHouseClient = clickHouseClient;
        _logger = logger;
    }

    public async Task IngestAsync(ClassifiedEditEvent editEvent, CancellationToken ct = default)
    {
        //  1. Провалидировать входной editEvent
        //  2. Убедиться что схема ClickHouse создана (опционально, lazy init)
        //  3. Записать событие в ClickHouse через _clickHouseClient.InsertEditAsync
        //  4. Логировать успешную запись на Debug
        //  5. В случае ошибок: логировать и не пробрасывать наружу (handler решит)
        if (!IsValidEvent(editEvent))
        {
            _logger.LogWarning("Skipping invalid event. ID: {Id}", editEvent?.EventId ?? "null");
            return;
        }
        if (!_isSchemaInitialized)
        {
            await InitializeSchemaOnceAsync(ct);
        }
        try
        {
            await _clickHouseClient.InsertEditAsync(editEvent, ct);
            _logger.LogDebug("Event {EventId} queued for ingestion", editEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest event {EventId}", editEvent.EventId);
        }
    }


    private async Task InitializeSchemaOnceAsync(CancellationToken ct)
    {
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (!_isSchemaInitialized)
            {
                _logger.LogInformation("Initializing ClickHouse schema");
                await _clickHouseClient.EnsureSchemaAsync(ct);
                _isSchemaInitialized = true;
                _logger.LogInformation("ClickHouse schema initialized successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Could not initialize ClickHouse schema. Data ingestion may fail.");
            throw;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
    private bool IsValidEvent(ClassifiedEditEvent evt)
    {
        if (evt == null) return false;
        if (string.IsNullOrWhiteSpace(evt.EventId)) return false;
        if (evt.Embedding == null) return false;

        return true;
    }
}
