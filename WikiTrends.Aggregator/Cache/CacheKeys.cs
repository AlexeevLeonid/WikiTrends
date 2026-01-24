using WikiTrends.Contracts.Events;

namespace WikiTrends.Aggregator.Cache;

public static class CacheKeys
{
    public static string GetTrendsKey(TrendPeriod period)
    {
        // TODO: 1. Сформировать ключ кэша для трендов по периоду
        // TODO: 2. Учитывать версионирование схемы ключей
        return $"v3:trends:{period.ToString().ToLowerInvariant()}";
    }

    public static string GetTopicDetailsKey(int topicId, TrendPeriod period)
    {
        // TODO: 1. Сформировать ключ кэша для деталей темы по topicId и периоду
        return $"v2:topic:{topicId}:{period.ToString().ToLowerInvariant()}";
    }
}
