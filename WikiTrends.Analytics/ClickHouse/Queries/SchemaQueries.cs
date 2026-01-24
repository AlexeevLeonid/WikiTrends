using WikiTrends.Analytics.Models;

namespace WikiTrends.Analytics.ClickHouse.Queries;

public static class SchemaQueries
{
    public static IReadOnlyList<string> GetSchemaDdl()
    {
        //  1. Вернуть список DDL запросов для создания БД/таблиц/индексов
        //  2. Учесть таблицы для событий, baseline и агрегаций
        var eventTableQuery = """
            CREATE TABLE IF NOT EXISTS edit_events
            (
                EventId String,
                WikiEditId UInt64,
                ArticleId UInt64,
                Title String,
                Wiki LowCardinality(String),
                Topics Nested
                (
                    TopicId Int32,
                    Name String,
                    Path String,
                    Confidence Float32
                ),
                Embedding Array(Float32),
                Timestamp DateTime,
                ClassifiedAt DateTime
            ) ENGINE = MergeTree()
            ORDER BY (Wiki, Timestamp)
            """;

        
        var baselineTableQuery = """
            CREATE TABLE IF NOT EXISTS BaselineData
            (
                TopicId UInt32,
                BaselineDaily Float32,
                CalculatedAt DateTime
            ) ENGINE = ReplacingMergeTree(CalculatedAt)
            ORDER BY (TopicId)
            """;

        var trendDataTableQuery = """
            CREATE TABLE IF NOT EXISTS TrendData
            (
                TopicId UInt32,
                TrendPeriod Enum8('LastHour' = 0, 'Last24Hours' = 1, 'Last7Days' = 2),
                EditCount UInt32,
                UniqueEditors UInt32,
                CalculatedAt DateTime DEFAULT now()
            ) ENGINE = MergeTree()
            ORDER BY (TrendPeriod, TopicId, CalculatedAt)
            """;
        return new List<string> { eventTableQuery, baselineTableQuery, trendDataTableQuery };
    }
}
