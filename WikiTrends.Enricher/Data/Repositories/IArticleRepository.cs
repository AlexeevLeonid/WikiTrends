using WikiTrends.Enricher.Data.Entities;

namespace WikiTrends.Enricher.Data.Repositories;

public interface IArticleRepository
{
    Task<ArticleEntity?> GetByWikiPageIdAsync(long wikiPageId, string wiki, CancellationToken ct = default);

    Task<ArticleEntity> UpsertAsync(ArticleEntity article, CancellationToken ct = default);

    Task ReplaceCategoriesAsync(long articleId, IReadOnlyList<string> categories, CancellationToken ct = default);
}
