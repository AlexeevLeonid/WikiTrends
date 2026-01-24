using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public interface IArticleTopicRepository
{
    Task<IReadOnlyList<ArticleTopicEntity>> GetByArticleIdAsync(long articleId, CancellationToken ct = default);

    Task ReplaceAsync(long articleId, IReadOnlyList<ArticleTopicEntity> mappings, CancellationToken ct = default);
}
