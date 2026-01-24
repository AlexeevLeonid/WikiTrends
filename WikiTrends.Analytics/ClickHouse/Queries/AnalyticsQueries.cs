using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.ClickHouse.Queries;

public static class AnalyticsQueries
{
    public static string GetTrendAggregationQuery(TrendPeriod period)
    {
        //  1. На основе period сформировать SQL агрегирования по topicId
        //  2. Вернуть SQL строку
        var window = period switch
        {
            TrendPeriod.LastHour => "1 HOUR",
            TrendPeriod.Last24Hours => "24 HOUR",
            TrendPeriod.Last7Days => "7 DAY",
            _ => "24 HOUR"
        };

        return $"""
            SELECT
                topicId as TopicId,
                @period as TrendPeriod,
                toInt32(count()) as EditCount,
                toInt32(0) as UniqueEditors
            FROM edit_events
            ARRAY JOIN Topics.TopicId as topicId
            WHERE greatest(Timestamp, ClassifiedAt) >= now() - INTERVAL {window}
            GROUP BY topicId
            ORDER BY EditCount DESC
            """;
    }

    public static string GetBaselineQuery()
    {
        //  1. На основе period сформировать SQL агрегирования по topicId
        //  2. Вернуть SQL строку
        return """
            SELECT toInt32(TopicId) as TopicId, BaselineDaily, CalculatedAt
            FROM BaselineData
            WHERE TopicId = @topicId
            ORDER BY CalculatedAt DESC
            LIMIT 1
            """;
    }

    public static string GetRecalculateBaselineQuery(int daysToLookBack)
    {
        return $"""
            SELECT 
                TopicId,
                avg(daily_count) as AvgDailyEvents
            FROM 
            (
                SELECT 
                    TopicId,
                    toDate(Timestamp) as Day,
                    count() as daily_count
                FROM edit_events
                ARRAY JOIN Topics.TopicId as TopicId
                WHERE greatest(Timestamp, ClassifiedAt) >= now() - INTERVAL {daysToLookBack} DAY
                GROUP BY TopicId, Day
            )
            GROUP BY TopicId
            """;
    }
}
