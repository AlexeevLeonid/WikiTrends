using WikiTrends.Classifier.Data.Entities;
using WikiTrends.Classifier.Data.Repositories;

namespace WikiTrends.Classifier.Seed;

public sealed class TopicSeeder
{
    private readonly ITopicRepository _topicRepository;
    private readonly ILogger<TopicSeeder> _logger;

    public TopicSeeder(
        ITopicRepository topicRepository,
        ILogger<TopicSeeder> logger)
    {
        _topicRepository = topicRepository;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        //  1. Определить список топиков по умолчанию (Name/Path)
        //  2. Для каждого топика сделать Upsert через _topicRepository.UpsertAsync
        //  3. Залогировать количество добавленных/обновлённых топиков
        //  4. Обработать ошибки (логировать и продолжать/прерывать по решению)
        var defaultTopics = new List<TopicEntity>
        {
            new() { Name = "Science", Path = "Root/Science" },
            new() { Name = "Technology", Path = "Root/Technology" },
            new() { Name = "Politics", Path = "Root/Politics" },
            new() { Name = "Business", Path = "Root/Business" },
            new() { Name = "Sports", Path = "Root/Sports" },
            new() { Name = "Culture", Path = "Root/Culture" },
            new() { Name = "History", Path = "Root/History" }
        };

        var existing = await _topicRepository.GetAllAsync(ct);
        var existingPaths = new HashSet<string>(existing.Select(x => x.Path));

        var added = 0;
        var updated = 0;

        foreach (var topic in defaultTopics)
        {
            try
            {
                var isExisting = existingPaths.Contains(topic.Path);
                await _topicRepository.UpsertAsync(topic, ct);
                if (isExisting)
                {
                    updated++;
                }
                else
                {
                    added++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed topic {TopicPath}", topic.Path);
            }
        }

        _logger.LogInformation("Seeded topics. Added {Added}, Updated {Updated}", added, updated);
    }
}
