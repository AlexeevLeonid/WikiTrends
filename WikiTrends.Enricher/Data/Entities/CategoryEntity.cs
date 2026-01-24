using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Enricher.Data.Entities;

public sealed class CategoryEntity : AuditableEntity
{
    public required string Name { get; set; }

    public long ArticleId { get; set; }
    public ArticleEntity? Article { get; set; }
}
