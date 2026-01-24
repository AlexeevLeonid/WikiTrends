namespace WikiTrends.Collector;

/// <summary>
/// Константы сервиса Collector.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Имя сервиса для логирования и метрик.
    /// </summary>
    public const string ServiceName = "WikiCollector";

    /// <summary>
    /// Kafka topics.
    /// </summary>
    public static class Topics
    {
        public const string RawEdits = "wiki.raw-edits";
    }

    /// <summary>
    /// Интервалы для логирования статистики.
    /// </summary>
    public static class Intervals
    {
        public const int LogStatisticsEveryNEvents = 1000;
    }
}