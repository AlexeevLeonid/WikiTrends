using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public interface ITopicRepository
{
    Task<IReadOnlyList<TopicEntity>> GetAllAsync(CancellationToken ct = default);

    Task<TopicEntity?> GetByIdAsync(long topicId, CancellationToken ct = default);

    Task<TopicEntity> UpsertAsync(TopicEntity topic, CancellationToken ct = default);
}
