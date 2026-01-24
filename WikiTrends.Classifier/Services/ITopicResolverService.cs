using WikiTrends.Classifier.Data.Entities;
using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public interface ITopicResolverService
{
    Task<Result<ArticleTopicEntity>> ResolveAndSaveTopicAsync(long articleId, string title, string lang, CancellationToken ct = default);
}
