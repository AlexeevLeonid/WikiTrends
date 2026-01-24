using WikiTrends.Contracts.Common;

namespace WikiTrends.Aggregator.DataSources;

public interface IClassifierDataSource
{
    Task<Result<object>> GetTopicDictionaryAsync(CancellationToken ct = default);
}
