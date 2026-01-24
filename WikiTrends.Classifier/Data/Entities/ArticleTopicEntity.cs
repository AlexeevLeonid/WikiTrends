using WikiTrends.Infrastructure.Persistence;

namespace WikiTrends.Classifier.Data.Entities;

public sealed class ArticleTopicEntity : AuditableEntity
{
    public long ArticleId { get; set; }
    public long TopicId { get; set; }
    public float Confidence { get; set; }

    public TopicEntity? Topic { get; set; }
}
