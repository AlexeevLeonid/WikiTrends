using System.Text.Json.Serialization;

namespace WikiTrends.Collector.Models;

/// <summary>
/// Модель события recent change из Wikipedia EventStreams SSE.
/// Полностью соответствует JSON-структуре из https://stream.wikimedia.org/v2/stream/recentchange
/// </summary>
public sealed class WikiRecentChange
{
    /// <summary>
    /// Уникальный ID изменения в потоке.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Тип изменения: "edit", "new", "log", "categorize", "external".
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Namespace статьи. 0 = основное пространство статей.
    /// </summary>
    [JsonPropertyName("namespace")]
    public int Namespace { get; set; }

    /// <summary>
    /// Заголовок страницы.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Имя пользователя, сделавшего правку.
    /// </summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    /// Правка сделана ботом.
    /// </summary>
    [JsonPropertyName("bot")]
    public bool Bot { get; set; }

    /// <summary>
    /// Незначительная правка.
    /// </summary>
    [JsonPropertyName("minor")]
    public bool Minor { get; set; }

    /// <summary>
    /// Информация о длине статьи до и после правки.
    /// </summary>
    [JsonPropertyName("length")]
    public WikiLength? Length { get; set; }

    /// <summary>
    /// Информация о ревизиях.
    /// </summary>
    [JsonPropertyName("revision")]
    public WikiRevision? Revision { get; set; }

    /// <summary>
    /// Имя сервера (например, "en.wikipedia.org").
    /// </summary>
    [JsonPropertyName("server_name")]
    public string? ServerName { get; set; }

    /// <summary>
    /// URL сервера.
    /// </summary>
    [JsonPropertyName("server_url")]
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Идентификатор wiki (например, "enwiki", "ruwiki").
    /// </summary>
    [JsonPropertyName("wiki")]
    public string? Wiki { get; set; }

    /// <summary>
    /// Unix timestamp события.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Комментарий к правке.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    /// <summary>
    /// Идентификатор страницы в пределах wiki.
    /// </summary>
    [JsonPropertyName("page_id")]
    public long? PageId { get; set; }

    /// <summary>
    /// Мета-информация события.
    /// </summary>
    [JsonPropertyName("meta")]
    public WikiMeta? Meta { get; set; }

    /// <summary>
    /// Это патрулированная правка.
    /// </summary>
    [JsonPropertyName("patrolled")]
    public bool Patrolled { get; set; }

    /// <summary>
    /// Парсированный комментарий (HTML).
    /// </summary>
    [JsonPropertyName("parsedcomment")]
    public string? ParsedComment { get; set; }
}

/// <summary>
/// Информация о длине страницы.
/// </summary>
public sealed class WikiLength
{
    /// <summary>
    /// Длина до правки (в байтах).
    /// </summary>
    [JsonPropertyName("old")]
    public int? Old { get; set; }

    /// <summary>
    /// Длина после правки (в байтах).
    /// </summary>
    [JsonPropertyName("new")]
    public int? New { get; set; }
}

/// <summary>
/// Информация о ревизиях.
/// </summary>
public sealed class WikiRevision
{
    /// <summary>
    /// ID старой ревизии.
    /// </summary>
    [JsonPropertyName("old")]
    public long? Old { get; set; }

    /// <summary>
    /// ID новой ревизии.
    /// </summary>
    [JsonPropertyName("new")]
    public long? New { get; set; }
}

/// <summary>
/// Мета-информация события из EventStreams.
/// </summary>
public sealed class WikiMeta
{
    /// <summary>
    /// Уникальный ID события в потоке.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// ISO 8601 timestamp.
    /// </summary>
    [JsonPropertyName("dt")]
    public string? Dt { get; set; }

    /// <summary>
    /// Домен источника.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    /// <summary>
    /// URI потока.
    /// </summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    /// <summary>
    /// URI запроса.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    /// <summary>
    /// Partition Kafka топика источника.
    /// </summary>
    [JsonPropertyName("partition")]
    public int? Partition { get; set; }

    /// <summary>
    /// Offset в Kafka топике источника.
    /// </summary>
    [JsonPropertyName("offset")]
    public long? Offset { get; set; }

    /// <summary>
    /// Тема/schema события.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
}