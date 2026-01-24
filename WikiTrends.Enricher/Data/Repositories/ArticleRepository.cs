using Microsoft.EntityFrameworkCore;
using WikiTrends.Enricher.Data.Entities;

namespace WikiTrends.Enricher.Data.Repositories;

public sealed class ArticleRepository : IArticleRepository
{
    private readonly EnricherDbContext _db;
    private readonly ILogger<ArticleRepository> _logger;

    public ArticleRepository(
        EnricherDbContext db,
        ILogger<ArticleRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ArticleEntity?> GetByWikiPageIdAsync(long wikiPageId, string wiki, CancellationToken ct = default)
    {
        if (wikiPageId <= 0) throw new ArgumentException("wikiPageId < 0");
        if (string.IsNullOrEmpty(wiki)) throw new ArgumentNullException("wiki is empty");
        return await _db.Articles.Include(x => x.Categories).FirstOrDefaultAsync(x => x.WikiPageId == wikiPageId && x.Wiki == wiki, ct);
    }

    public async Task<ArticleEntity> UpsertAsync(ArticleEntity article, CancellationToken ct = default)
    {
        if (article == null)
            throw new ArgumentNullException(nameof(article));
        if (string.IsNullOrEmpty(article.Title))
            throw new ArgumentException("Title is required", nameof(article));
        if (article.WikiPageId <= 0)
            throw new ArgumentException("WikiPageId must be positive", nameof(article));
        if (string.IsNullOrEmpty(article.Wiki))
            throw new ArgumentException("Wiki is required", nameof(article));

        var dbArticle = await GetByWikiPageIdAsync(article.WikiPageId, article.Wiki, ct);

        if (dbArticle != null)
        {
            dbArticle.Title = article.Title;
            dbArticle.Extract = article.Extract;
            dbArticle.LastEnrichedAt = DateTimeOffset.UtcNow;

            _logger.LogDebug("Updating article {WikiPageId} in {Wiki}",
                article.WikiPageId, article.Wiki);
        }
        else
        {
            article.LastEnrichedAt = DateTimeOffset.UtcNow;
            var entry = await _db.Articles.AddAsync(article, ct);
            dbArticle = entry.Entity;

            _logger.LogDebug("Inserting new article {WikiPageId} in {Wiki}",
                article.WikiPageId, article.Wiki);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted article Id={Id}, WikiPageId={WikiPageId}",
            dbArticle.Id, dbArticle.WikiPageId);

        return dbArticle;

    }

    public async Task ReplaceCategoriesAsync(long articleId, IReadOnlyList<string> categories, CancellationToken ct = default)
    {
        if (articleId <= 0) throw new ArgumentException("articleId < 0");
        categories ??= Array.Empty<string>();

        var existing = await _db.Categories
            .Where(c => c.ArticleId == articleId)
            .ToListAsync(ct);

        var existingNames = existing.Select(c => c.Name).ToHashSet();
        var newNames = categories.Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet();

        var toRemove = existing.Where(c => !newNames.Contains(c.Name)).ToList();

        var toAdd = newNames
            .Where(name => !existingNames.Contains(name))
            .Select(name => new CategoryEntity { Name = name, ArticleId = articleId })
            .ToList();

        if (toRemove.Any())
            _db.Categories.RemoveRange(toRemove);

        if (toAdd.Any())
            await _db.Categories.AddRangeAsync(toAdd, ct);

        if (toRemove.Any() || toAdd.Any())
            await _db.SaveChangesAsync(ct);

    }
}
