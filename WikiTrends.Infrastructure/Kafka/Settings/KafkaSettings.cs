namespace WikiTrends.Infrastructure.Kafka.Settings;

/// <summary>
/// Настройки подключения к Apache Kafka.
/// Используется через IOptions pattern.
/// </summary>
public sealed class KafkaSettings
{
    /// <summary>
    /// Секция конфигурации в appsettings.json
    /// </summary>
    public const string SectionName = "Kafka";

    /// <summary>
    /// Адреса брокеров Kafka (например: "localhost:9092" или "broker1:9092,broker2:9092")
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public required string BootstrapServers { get; init; }

    /// <summary>
    /// Идентификатор consumer group для балансировки нагрузки между инстансами
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public required string GroupId { get; init; }

    /// <summary>
    /// Откуда начинать чтение при отсутствии сохранённого offset: "earliest" или "latest"
    /// </summary>
    public string AutoOffsetReset { get; init; } = "earliest";

    /// <summary>
    /// Автоматический коммит offset'ов. false = ручное управление для надёжности
    /// </summary>
    public bool EnableAutoCommit { get; init; } = false;

    /// <summary>
    /// Интервал автокоммита в миллисекундах (если EnableAutoCommit = true)
    /// </summary>
    public int AutoCommitIntervalMs { get; init; } = 5000;

    /// <summary>
    /// Таймаут сессии consumer'а в миллисекундах
    /// </summary>
    public int SessionTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Максимальное время ожидания при poll в миллисекундах
    /// </summary>
    public int MaxPollIntervalMs { get; init; } = 300000;

    /// <summary>
    /// Включить логирование метрик Kafka
    /// </summary>
    public bool MetricsEnabled { get; init; } = true;

    /// <summary>
    /// Интервал логирования метрик Kafka в секундах
    /// </summary>
    public int MetricsLogIntervalSeconds { get; init; } = 10;

    /// <summary>
    /// Порог медленных сообщений в миллисекундах
    /// </summary>
    public int SlowMessageThresholdMs { get; init; } = 2000;
}