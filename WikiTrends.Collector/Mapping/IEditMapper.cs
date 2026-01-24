using WikiTrends.Collector.Models;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Collector.Mapping;

/// <summary>
/// Маппер для преобразования Wikipedia событий в доменные события.
/// </summary>
public interface IEditMapper
{
    /// <summary>
    /// Преобразовать WikiRecentChange в RawEditEvent.
    /// Возвращает null, если событие не проходит фильтры.
    /// </summary>
    /// <param name="change">Событие из Wikipedia.</param>
    /// <returns>RawEditEvent или null.</returns>
    RawEditEvent? Map(WikiRecentChange change);
}