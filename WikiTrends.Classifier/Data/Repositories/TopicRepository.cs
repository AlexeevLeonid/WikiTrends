 using Microsoft.EntityFrameworkCore;
using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public sealed class TopicRepository : ITopicRepository
{
    private readonly ClassifierDbContext _db;
    private readonly ILogger<TopicRepository> _logger;

    public TopicRepository(
        ClassifierDbContext db,
        ILogger<TopicRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TopicEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Topics
            .OrderBy(x => x.Path)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<TopicEntity?> GetByIdAsync(long topicId, CancellationToken ct = default)
    {
        if (topicId <= 0)
        {
            throw new ArgumentException("TopicId must be positive", nameof(topicId));
        }
        return await _db.Topics.FindAsync(new object[] { topicId }, ct);
    }

    public async Task<TopicEntity> UpsertAsync(TopicEntity topic, CancellationToken ct = default)
    {
        if (topic == null)
        {
            throw new ArgumentNullException(nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(topic.Name))
        {
            throw new ArgumentException("Topic name is required", nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(topic.Path))
        {
            throw new ArgumentException("Topic path is required", nameof(topic));
        }

        var existingTopic = await _db.Topics
            .FirstOrDefaultAsync(x => x.Path == topic.Path, ct);

        TopicEntity resultEntity;

        if (existingTopic != null)
        {
            existingTopic.Name = topic.Name;

            resultEntity = existingTopic;
        }
        else
        {
            try
            {
                var entry = await _db.Topics.AddAsync(topic, ct);
                resultEntity = entry.Entity;
            }
            catch (DbUpdateException)
            {
                var concurrent = await _db.Topics
                    .FirstOrDefaultAsync(x => x.Path == topic.Path, ct);

                if (concurrent == null) throw;

                concurrent.Name = topic.Name;
                resultEntity = concurrent;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var concurrent = await _db.Topics
                .FirstOrDefaultAsync(x => x.Path == topic.Path, ct);

            if (concurrent == null) throw;

            concurrent.Name = topic.Name;
            await _db.SaveChangesAsync(ct);
            resultEntity = concurrent;
        }

        return resultEntity;
    }
}
