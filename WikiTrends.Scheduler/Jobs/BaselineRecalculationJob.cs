using WikiTrends.Contracts.Events;
using WikiTrends.Scheduler.Services;

namespace WikiTrends.Scheduler.Jobs;

public sealed class BaselineRecalculationJob
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly ILogger<BaselineRecalculationJob> _logger;

    public BaselineRecalculationJob(
        ICommandPublisher commandPublisher,
        ILogger<BaselineRecalculationJob> logger)
    {
        _commandPublisher = commandPublisher;
        _logger = logger;
    }

    public async Task ExecuteAsync(int? topicId, CancellationToken ct = default)
    {
        // TODO: 1. Сформировать RecalculateBaselineCommand (topicId может быть null = все темы)
        // TODO: 2. Установить RequestedAt = DateTimeOffset.UtcNow
        // TODO: 3. Опубликовать команду через _commandPublisher.PublishRecalculateBaselineAsync
        // TODO: 4. Логировать выполнение джобы
        var command = new RecalculateBaselineCommand
        {
            TopicId = topicId,
            RequestedAt = DateTimeOffset.UtcNow
        };

        await _commandPublisher.PublishRecalculateBaselineAsync(command, ct);

        _logger.LogInformation(
            "Baseline recalculation job executed. TopicId={TopicId}. RequestedAt={RequestedAt}",
            command.TopicId,
            command.RequestedAt);
    }
}
