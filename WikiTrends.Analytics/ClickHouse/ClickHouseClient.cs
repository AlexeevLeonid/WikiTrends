using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Options;
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

    private async Task<TResult> ExecuteWithConnectionAsync<TResult>(
        string operation,
        Func<ClickHouseConnection, Task<TResult>> action,
        CancellationToken ct)
    {
        try
        {
            await using var connection = new ClickHouseConnection(_settings.ConnectionString);
            await connection.OpenAsync(ct);
            return await action(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClickHouse operation failed: {Operation}", operation);
            throw;
        }
    }

    private Task ExecuteWithConnectionAsync(
        string operation,
        Func<ClickHouseConnection, Task> action,
        CancellationToken ct)
        => ExecuteWithConnectionAsync<object>(operation, async c =>
        {
            await action(c);
            return new object();
        }, ct);

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        //  1. Сформировать DDL запросы из SchemaQueries
        //  2. Выполнить их по порядку через HTTP интерфейс ClickHouse
        //  3. Логировать успешную инициализацию
        var queries = SchemaQueries.GetSchemaDdl();
        await ExecuteWithConnectionAsync(
            nameof(EnsureSchemaAsync),
            async connection =>
            {
                foreach (var query in queries)
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = query;
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            },
            ct);

        _logger.LogInformation("Database has created");
    }



    public async Task InsertEditAsync(ClassifiedEditEvent editEvent, CancellationToken ct = default)
    {
        //  1. Провалидировать editEvent
        //  2. Подготовить INSERT запрос (InsertQueries)
        //  3. Выполнить запрос через HTTP API ClickHouse
        //  4. Обработать ошибки и логирование
        if (editEvent == null)
        {
            throw new ArgumentNullException(nameof(editEvent));
        }

        var topics = editEvent.Topics ?? new List<TopicScore>();
        var tIds = topics.Select(x => x.TopicId).ToArray();
        var tNames = topics.Select(x => x.TopicName).ToArray();
        var tPaths = topics.Select(x => x.TopicPath).ToArray();
        var tConfs = topics.Select(x => x.Confidence).ToArray();

        _eventsBuffer.Add(new object[]
        {
            editEvent.EventId,
            editEvent.WikiEditId,
            editEvent.ArticleId,
            editEvent.Title,
            editEvent.Wiki,
            tIds, tNames, tPaths, tConfs,
            editEvent.Embedding ?? Array.Empty<float>(),
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

        var batch = _eventsBuffer.ToList();

        await ExecuteWithConnectionAsync(
            "FlushEditEvents",
            async connection =>
            {
                using var bulk = new ClickHouseBulkCopy(connection)
                {
                    DestinationTableName = editTablName,
                    BatchSize = batch.Count
                };
                await bulk.InitAsync();
                await bulk.WriteToServerAsync(batch, ct);
            },
            ct);

        _eventsBuffer.Clear();
        _lastEventsFlush = DateTime.UtcNow;
    }

    public async Task<IReadOnlyList<TrendData>> QueryTrendsAsync(TrendPeriod period, CancellationToken ct = default)
    {
        //  1. Подготовить аналитический запрос для указанного периода (AnalyticsQueries)
        //  2. Выполнить запрос, распарсить результат в список TrendData
        //  3. Вернуть список
        return await ExecuteWithConnectionAsync(
            nameof(QueryTrendsAsync),
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = AnalyticsQueries.GetTrendAggregationQuery(period);
                command.Parameters.Add(new ClickHouseDbParameter
                {
                    ParameterName = "period",
                    Value = (int)period,
                    DbType = System.Data.DbType.Int32
                });

                var result = new List<TrendData>();
                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    result.Add(new TrendData
                    {
                        TopicId = reader.GetInt32(0),
                        Period = ParseTrendPeriod(reader.GetValue(1)),
                        EditCount = reader.GetInt32(2),
                        UniqueEditors = reader.GetInt32(3)
                    });
                }

                return (IReadOnlyList<TrendData>)result;
            },
            ct);
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

        return await ExecuteWithConnectionAsync(
            nameof(GetTopicInfoAsync),
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = TopicQueries.GetTopicInfoQuery(ids);

                var result = new Dictionary<int, (string Name, string Path)>();
                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var topicId = reader.GetInt32(0);
                    var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var path = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    result[topicId] = (name, path);
                }

                return (IReadOnlyDictionary<int, (string Name, string Path)>)result;
            },
            ct);
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

        return await ExecuteWithConnectionAsync(
            nameof(GetBaselineAsync),
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = AnalyticsQueries.GetBaselineQuery();
                command.Parameters.Add(new ClickHouseDbParameter
                {
                    ParameterName = "topicId",
                    Value = (int)topicId,
                    DbType = System.Data.DbType.Int32
                });

                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    return new BaselineData
                    {
                        TopicId = reader.GetInt32(0),
                        BaselineDaily = reader.GetFloat(1),
                        CalculatedAt = reader.GetDateTime(2)
                    };
                }

                return null;
            },
            ct);
    }

    public async Task UpsertBaselineAsync(BaselineData baseline, CancellationToken ct = default)
    {
        //  1. Выполнить UPSERT baseline (insert/update) в ClickHouse
        //  2. Логировать результат
        await ExecuteWithConnectionAsync(
            nameof(UpsertBaselineAsync),
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = InsertQueries.GetUpsertBaselineQuery();

                command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "topicId", Value = baseline.TopicId });
                command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "baselineDaily", Value = baseline.BaselineDaily });
                command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "calcAt", Value = baseline.CalculatedAt.ToUniversalTime() });

                await command.ExecuteNonQueryAsync(ct);
            },
            ct);

        _logger.LogInformation(
            "Baseline upserted successfully for TopicId: {TopicId}, Value: {Value}",
            baseline.TopicId,
            baseline.BaselineDaily);
    }

    public async Task<List<BaselineData>> ComputeBaselinesFromHistoryAsync(int daysToLookBack, CancellationToken ct)
    {
        return await ExecuteWithConnectionAsync(
            nameof(ComputeBaselinesFromHistoryAsync),
            async connection =>
            {
                var result = new List<BaselineData>();

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
            },
            ct);
    }

    public async Task BulkUpsertBaselinesAsync(IEnumerable<BaselineData> baselines, CancellationToken ct)
    {
        var data = baselines
            .Select(b => new object[] { b.TopicId, b.BaselineDaily, b.CalculatedAt })
            .ToList();

        if (data.Count == 0)
        {
            return;
        }

        await ExecuteWithConnectionAsync(
            nameof(BulkUpsertBaselinesAsync),
            async connection =>
            {
                using var bulk = new ClickHouseBulkCopy(connection)
                {
                    DestinationTableName = "BaselineData",
                    BatchSize = Math.Min(10000, data.Count)
                };

                await bulk.InitAsync();
                await bulk.WriteToServerAsync(data, ct);
            },
            ct);
    }

    public async Task<IReadOnlyList<ArticleTrend>> GetTopArticlesForTopicAsync(
    int topicId,
    TrendPeriod period,
    int limit,
    CancellationToken ct)
    {
        // Определяем глубину истории для SQL
        return await ExecuteWithConnectionAsync(
            nameof(GetTopArticlesForTopicAsync),
            async connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = ArticleQueries.GetTopArticlesForTopicQuery(period, limit);
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

                return (IReadOnlyList<ArticleTrend>)result;
            },
            ct);
    }
}
