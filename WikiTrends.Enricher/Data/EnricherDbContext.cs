using Microsoft.EntityFrameworkCore;
using WikiTrends.Enricher.Data.Entities;
using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Enricher.Data;

public sealed class EnricherDbContext : BaseDbContext
{
    public EnricherDbContext(DbContextOptions<EnricherDbContext> options)
        : base(options)
    {
    }

    public DbSet<ArticleEntity> Articles => Set<ArticleEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
}
