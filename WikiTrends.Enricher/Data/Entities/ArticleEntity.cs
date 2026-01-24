using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Enricher.Data.Entities;

public sealed class ArticleEntity : AuditableEntity
{
    public long WikiPageId { get; set; }
    public required string Title { get; set; }
    public required string Wiki { get; set; }

    public string? Extract { get; set; }
    public DateTimeOffset LastEnrichedAt { get; set; }

    public ICollection<CategoryEntity> Categories { get; set; } = new List<CategoryEntity>();
}
