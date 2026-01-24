using Microsoft.EntityFrameworkCore;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public sealed class WikidataMappingRepository : IWikidataMappingRepository
{
    private readonly ClassifierDbContext _db;

    public WikidataMappingRepository(ClassifierDbContext db)
    {
        _db = db;
    }

    public Task<WikidataMappingEntity?> GetAsync(string wiki, string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wiki)) throw new ArgumentException("Wiki is required", nameof(wiki));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

        return _db.WikidataMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Wiki == wiki && x.Title == title, ct);
    }

    public async Task<WikidataMappingEntity> UpsertAsync(string wiki, string title, string? wikidataId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wiki)) throw new ArgumentException("Wiki is required", nameof(wiki));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required", nameof(title));

        var existing = await _db.WikidataMappings
            .FirstOrDefaultAsync(x => x.Wiki == wiki && x.Title == title, ct);

        if (existing != null)
        {
            existing.WikidataId = wikidataId;
            existing.CachedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var entity = new WikidataMappingEntity
        {
            Wiki = wiki,
            Title = title,
            WikidataId = wikidataId,
            CachedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var entry = await _db.WikidataMappings.AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);
            return entry.Entity;
        }
        catch (DbUpdateException)
        {
            var concurrent = await _db.WikidataMappings
                .FirstOrDefaultAsync(x => x.Wiki == wiki && x.Title == title, ct);

            if (concurrent == null) throw;

            concurrent.WikidataId = wikidataId;
            concurrent.CachedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return concurrent;
        }
    }
}
