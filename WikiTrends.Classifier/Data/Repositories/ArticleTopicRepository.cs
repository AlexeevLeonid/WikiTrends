using Microsoft.EntityFrameworkCore;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public sealed class ArticleTopicRepository : IArticleTopicRepository
{
    private readonly ClassifierDbContext _db;
    private readonly ILogger<ArticleTopicRepository> _logger;

    public ArticleTopicRepository(
        ClassifierDbContext db,
        ILogger<ArticleTopicRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ArticleTopicEntity>> GetByArticleIdAsync(long articleId, CancellationToken ct = default)
    {
        //  1. Провалидировать articleId
        //  2. Загрузить связи для статьи из _db.ArticleTopics с Include(Topic)
        //  3. Вернуть список
        if (articleId <= 0)
        {
            throw new ArgumentException("ArticleId must be positive", nameof(articleId));
        }

        return await _db.ArticleTopics
            .Include(x => x.Topic)
            .Where(x => x.ArticleId == articleId)
            .ToListAsync(ct);
    }

    public async Task ReplaceAsync(long articleId, IReadOnlyList<ArticleTopicEntity> mappings, CancellationToken ct = default)
    {
        //  1. Провалидировать articleId и mappings
        //  2. Удалить существующие связи articleId -> topics
        //  3. Добавить новые связи
        //  4. Сохранить изменения

        if (articleId <= 0)
        {
            throw new ArgumentException("ArticleId must be positive", nameof(articleId));
        }

        mappings ??= Array.Empty<ArticleTopicEntity>();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await _db.ArticleTopics
                    .Where(x => x.ArticleId == articleId)
                    .ExecuteDeleteAsync(ct);

                if (mappings.Any())
                {
                    foreach (var item in mappings) item.ArticleId = articleId;
                    await _db.ArticleTopics.AddRangeAsync(mappings, ct);
                    await _db.SaveChangesAsync(ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
