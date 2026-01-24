using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.Copy;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NodaTime;
using System;
using WikiTrends.Analytics.ClickHouse.Queries;
using WikiTrends.Analytics.Models;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.ClickHouse;

public sealed class ClickHouseClient : IClickHouseClient, IDisposable, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ClickHouseSettings _settings;
    private readonly ILogger<ClickHouseClient> _logger;
    private readonly List<object[]> _eventsBuffer = new();
    private DateTime _lastEventsFlush = DateTime.UtcNow;
    private readonly int _batchSize = 100;
    private readonly TimeSpan _maxFlushInterval = TimeSpan.FromSeconds(5);
    private const string editTablName = "edit_events";

    public ClickHouseClient(
        HttpClient httpClient,
        IOptions<ClickHouseSettings> settings,
        ILogger<ClickHouseClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        //  1. Сформировать DDL запросы из SchemaQueries
        //  2. Выполнить их по порядку через HTTP интерфейс ClickHouse
        //  3. Логировать успешную инициализацию
        var queries = SchemaQueries.GetSchemaDdl();
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);
        try
        {
            foreach (var query in queries)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = query;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error on create database scheme {Error}", ex.Message);
            throw;
        }
        _logger.LogInformation("Database has created");
    }



    public async Task InsertEditAsync(ClassifiedEditEvent editEvent, CancellationToken ct = default)
    {
        //  1. Провалидировать editEvent
        //  2. Подготовить INSERT запрос (InsertQueries)
        //  3. Выполнить запрос через HTTP API ClickHouse
        //  4. Обработать ошибки и логирование
        var tIds = editEvent.Topics.Select(x => x.TopicId).ToArray();
        var tNames = editEvent.Topics.Select(x => x.TopicName).ToArray();
        var tPaths = editEvent.Topics.Select(x => x.TopicPath).ToArray();
        var tConfs = editEvent.Topics.Select(x => x.Confidence).ToArray();
        _eventsBuffer.Add(new object[]
        {
            editEvent.EventId,
            editEvent.WikiEditId,
            editEvent.ArticleId,
            editEvent.Title,
            editEvent.Wiki,
            tIds, tNames, tPaths, tConfs,
            editEvent.Embedding,
            editEvent.Timestamp.UtcDateTime,
            editEvent.ClassifiedAt.UtcDateTime
        });
        if (_eventsBuffer.Count >= _batchSize || (DateTime.UtcNow - _lastEventsFlush) >= _maxFlushInterval)
        {
            await FlushAsync(ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush ClickHouse buffer on dispose.");
        }
    }

    public void Dispose()
    {
        try
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush ClickHouse buffer on dispose.");
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_eventsBuffer.Count == 0)
        {
            return;
        }

        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);
        using var bulk = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = editTablName,
            BatchSize = _eventsBuffer.Count
        };
        await bulk.InitAsync();
        await bulk.WriteToServerAsync(_eventsBuffer, ct);
        _eventsBuffer.Clear();
        _lastEventsFlush = DateTime.UtcNow;
    }

    public async Task<IReadOnlyList<TrendData>> QueryTrendsAsync(TrendPeriod period, CancellationToken ct = default)
    {
        //  1. Подготовить аналитический запрос для указанного периода (AnalyticsQueries)
        //  2. Выполнить запрос, распарсить результат в список TrendData
        //  3. Вернуть список
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();

        command.CommandText = AnalyticsQueries.GetTrendAggregationQuery(period);

        command.Parameters.Add(new ClickHouseDbParameter
        {
            ParameterName = "period",
            Value = (int)period,
            DbType = System.Data.DbType.Int32
        });

        var result = new List<TrendData>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new TrendData
                {
                    TopicId = reader.GetInt32(0),
                    Period = ParseTrendPeriod(reader.GetValue(1)),

                    EditCount = reader.GetInt32(2),
                    UniqueEditors = reader.GetInt32(3)
                };
                result.Add(row);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query trends for period {Period}", period);
            throw;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<int, (string Name, string Path)>> GetTopicInfoAsync(IEnumerable<int> topicIds, CancellationToken ct = default)
    {
        if (topicIds == null)
        {
            return new Dictionary<int, (string Name, string Path)>();
        }

        var ids = topicIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<int, (string Name, string Path)>();
        }

        var inList = string.Join(",", ids);

        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT
                tid,
                coalesce(
                    argMaxIf(name, greatest(Timestamp, ClassifiedAt), name != '' AND NOT match(lower(name), '^topic\\s*\\d+$') AND NOT match(lower(name), '^topic\\d+$')),
                    argMax(name, greatest(Timestamp, ClassifiedAt))
                ) as name,
                coalesce(
                    argMaxIf(path, greatest(Timestamp, ClassifiedAt), path != '' AND NOT match(lower(path), '^topic-\\d+$')),
                    argMax(path, greatest(Timestamp, ClassifiedAt))
                ) as path
            FROM
            (
                SELECT
                    tid,
                    name,
                    path,
                    Timestamp,
                    ClassifiedAt
                FROM edit_events
                ARRAY JOIN
                    Topics.TopicId AS tid,
                    Topics.Name AS name,
                    Topics.Path AS path
                WHERE tid IN ({inList})
            )
            GROUP BY tid";

        var result = new Dictionary<int, (string Name, string Path)>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var topicId = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var path = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                result[topicId] = (name, path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query topic info for ids [{Ids}]", inList);
            throw;
        }

        return result;
    }

    private TrendPeriod ParseTrendPeriod(object dbValue)
    {
        if (dbValue is int iVal) return (TrendPeriod)iVal;
        if (dbValue is string sVal)
        {
            return Enum.Parse<TrendPeriod>(sVal); // Или свой маппинг
        }
        // Если вернулся байт или long
        return (TrendPeriod)Convert.ToInt32(dbValue);
    }

    public async Task<BaselineData?> GetBaselineAsync(int topicId, CancellationToken ct = default)
    {
        //  1. Выполнить запрос baseline по topicId
        //  2. Вернуть BaselineData или null
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();

        command.CommandText = AnalyticsQueries.GetBaselineQuery();

        command.Parameters.Add(new ClickHouseDbParameter
        {
            ParameterName = "topicId",
            Value = (int)topicId,
            DbType = System.Data.DbType.Int32
        });

        var result = new List<BaselineData>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new BaselineData
                {
                    TopicId = reader.GetInt32(0),
                    BaselineDaily = reader.GetFloat(1),
                    CalculatedAt = reader.GetDateTime(2)
                };
                return row;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query baseline for topicId {TopicId}", topicId);
            throw;
        }

    }

    public async Task UpsertBaselineAsync(BaselineData baseline, CancellationToken ct = default)
    {
        //  1. Выполнить UPSERT baseline (insert/update) в ClickHouse
        //  2. Логировать результат
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();

        command.CommandText = InsertQueries.GetUpsertBaselineQuery();

        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "topicId", Value = baseline.TopicId });
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "baselineDaily", Value = baseline.BaselineDaily });
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "calcAt", Value = baseline.CalculatedAt.ToUniversalTime() });

        try
        {
            await command.ExecuteNonQueryAsync(ct);
            _logger.LogInformation(
                "Baseline upserted successfully for TopicId: {TopicId}, Value: {Value}",
                baseline.TopicId,
                baseline.BaselineDaily);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert baseline for TopicId {TopicId}", baseline.TopicId);
            throw;
        }
    }

    public async Task<List<BaselineData>> ComputeBaselinesFromHistoryAsync(int daysToLookBack, CancellationToken ct)
    {
        var result = new List<BaselineData>();

        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);
        using var command = connection.CreateCommand();

        command.CommandText = AnalyticsQueries.GetRecalculateBaselineQuery(daysToLookBack);

        using var reader = await command.ExecuteReaderAsync(ct);
        var now = DateTime.UtcNow;

        while (await reader.ReadAsync(ct))
        {
            result.Add(new BaselineData
            {
                TopicId = reader.GetInt32(0),
                BaselineDaily = (float)reader.GetDouble(1),
                CalculatedAt = now
            });
        }

        return result;
    }

    public async Task BulkUpsertBaselinesAsync(IEnumerable<BaselineData> baselines, CancellationToken ct)
    {
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);

        using var bulk = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = "BaselineData",
            BatchSize = 10000
        };

        var data = baselines.Select(b => new object[]
        {
        b.TopicId,
        b.BaselineDaily,
        b.CalculatedAt
        });

        await bulk.WriteToServerAsync(data, ct);
    }

    public async Task<IReadOnlyList<ArticleTrend>> GetTopArticlesForTopicAsync(
    int topicId,
    TrendPeriod period,
    int limit,
    CancellationToken ct)
    {
        // Определяем глубину истории для SQL
        int hoursLookBack = period switch
        {
            TrendPeriod.LastHour => 1,
            TrendPeriod.Last24Hours => 24,
            TrendPeriod.Last7Days => 168,
            _ => 24
        };

        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(ct);
        using var command = connection.CreateCommand();

        // SQL: Найти топ 3 статьи по количеству правок внутри конкретного топика за период
        // Используем `arrayExists` так как Topics у нас Nested/Array
        var safeLimit = Math.Max(1, limit);
        command.CommandText = $@"
        SELECT 
            toInt64(ArticleId) as ArticleId,
            any(Title) as Title,
            toInt32(count()) as Edits
        FROM edit_events
        WHERE Timestamp >= now() - INTERVAL {hoursLookBack} HOUR
          AND has(Topics.TopicId, @topicId)
        GROUP BY ArticleId
        ORDER BY Edits DESC
        LIMIT {safeLimit}";

        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "topicId", Value = topicId });

        var result = new List<ArticleTrend>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ArticleTrend
            {
                ArticleId = reader.GetInt64(0),
                Title = reader.GetString(1),
                EditCount = reader.GetInt32(2),
                UniqueEditors = 0 
            });
        }
        return result;
    }
}
