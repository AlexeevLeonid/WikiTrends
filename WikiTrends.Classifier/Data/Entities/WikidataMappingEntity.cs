using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Classifier.Data.Entities;

public sealed class WikidataMappingEntity : AuditableEntity
{
    public required string Wiki { get; set; }
    public required string Title { get; set; }
    public string? WikidataId { get; set; }
    public DateTimeOffset CachedAt { get; set; }
}
