using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Classifier.Data.Entities;

public sealed class TopicEntity : AuditableEntity
{
    public required string Name { get; set; }
    public required string Path { get; set; }
}
