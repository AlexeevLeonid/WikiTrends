using Microsoft.EntityFrameworkCore;
using WikiTrends.Classifier.Data.Entities;
using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Classifier.Data;

public sealed class ClassifierDbContext : BaseDbContext
{
    public ClassifierDbContext(DbContextOptions<ClassifierDbContext> options)
        : base(options)
    {
    }

    public DbSet<TopicEntity> Topics => Set<TopicEntity>();
    public DbSet<ArticleTopicEntity> ArticleTopics => Set<ArticleTopicEntity>();
    public DbSet<WikidataMappingEntity> WikidataMappings => Set<WikidataMappingEntity>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateCachedAtFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateCachedAtFields()
    {
        // Находим все сущности, которые были добавлены или изменены
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is WikidataMappingEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            // Приводим к типу и обновляем время
            ((WikidataMappingEntity)entry.Entity).CachedAt = DateTime.UtcNow;
        }
    }
}
