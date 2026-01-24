using WikiTrends.Contracts.Common;

namespace WikiTrends.Aggregator.DataSources;

public interface IEnricherDataSource
{
    Task<Result<object>> GetArticleAsync(long articleId, CancellationToken ct = default);
}
