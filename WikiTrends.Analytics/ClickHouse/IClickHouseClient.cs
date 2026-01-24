using WikiTrends.Analytics.Models;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.ClickHouse;

public interface IClickHouseClient
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task InsertEditAsync(ClassifiedEditEvent editEvent, CancellationToken ct = default);

    Task<IReadOnlyList<TrendData>> QueryTrendsAsync(TrendPeriod period, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, (string Name, string Path)>> GetTopicInfoAsync(IEnumerable<int> topicIds, CancellationToken ct = default);

    Task<BaselineData?> GetBaselineAsync(int topicId, CancellationToken ct = default);

    Task UpsertBaselineAsync(BaselineData baseline, CancellationToken ct = default);

    Task<List<BaselineData>> ComputeBaselinesFromHistoryAsync(int daysToLookBack, CancellationToken ct);

    Task BulkUpsertBaselinesAsync(IEnumerable<BaselineData> baselines, CancellationToken ct);

    Task<IReadOnlyList<ArticleTrend>> GetTopArticlesForTopicAsync(int topicId, TrendPeriod period, int limit, CancellationToken ct);
}
