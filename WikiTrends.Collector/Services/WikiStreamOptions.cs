namespace WikiTrends.Collector.Services;

/// <summary>
/// Настройки подключения к Wikipedia EventStreams.
/// </summary>
public sealed class WikiStreamOptions
{
    public const string SectionName = "WikiStream";

    /// <summary>
    /// URL SSE потока Wikipedia.
    /// </summary>
    public string StreamUrl { get; set; } = "https://stream.wikimedia.org/v2/stream/recentchange";

    /// <summary>
    /// Список разрешённых wiki (например, ["enwiki", "ruwiki"]).
    /// </summary>
    public IReadOnlyList<string> AllowedWikis { get; set; } = ["enwiki", "ruwiki"];

    /// <summary>
    /// Разрешённые namespace (0 = основные статьи).
    /// </summary>
    public IReadOnlyList<int> AllowedNamespaces { get; set; } = [0];

    /// <summary>
    /// Разрешённые типы событий.
    /// </summary>
    public IReadOnlyList<string> AllowedTypes { get; set; } = ["edit"];

    /// <summary>
    /// Начальная задержка перед переподключением (секунды).
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Максимальная задержка переподключения (секунды).
    /// </summary>
    public int MaxReconnectDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Интервал health check (секунды).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}