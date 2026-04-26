using System;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.ClickHouse.Queries;

public static class ArticleQueries
{
    public static string GetTopArticlesForTopicQuery(TrendPeriod period, int limit)
    {
        var hoursLookBack = period switch
        {
            TrendPeriod.LastHour => 1,
            TrendPeriod.Last24Hours => 24,
            TrendPeriod.Last7Days => 168,
            _ => 24
        };

        var safeLimit = Math.Max(1, limit);

        return $@"
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
    }
}
