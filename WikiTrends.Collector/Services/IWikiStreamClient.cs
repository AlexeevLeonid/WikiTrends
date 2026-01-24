using WikiTrends.Collector.Models;

namespace WikiTrends.Collector.Services;

/// <summary>
/// Клиент для подключения к Wikipedia EventStreams SSE.
/// </summary>
public interface IWikiStreamClient : IAsyncDisposable
{
    /// <summary>
    /// Текущее состояние подключения.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Последний успешно прочитанный event ID (для resume).
    /// </summary>
    string? LastEventId { get; }

    /// <summary>
    /// Установить SSE соединение с Wikipedia EventStreams.
    /// </summary>
    /// <param name="lastEventId">Опциональный ID последнего события для продолжения потока.</param>
    /// <param name="ct">Токен отмены.</param>
    Task ConnectAsync(string? lastEventId = null, CancellationToken ct = default);

    /// <summary>
    /// Прочитать следующее событие из потока.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Событие или null, если поток закрыт.</returns>
    Task<WikiRecentChange?> ReadEventAsync(CancellationToken ct = default);

    /// <summary>
    /// Закрыть соединение.
    /// </summary>
    void Disconnect();
}