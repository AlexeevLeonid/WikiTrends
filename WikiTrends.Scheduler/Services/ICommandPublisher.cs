using WikiTrends.Contracts.Events;

namespace WikiTrends.Scheduler.Services;

public interface ICommandPublisher
{
    Task PublishRecalculateBaselineAsync(RecalculateBaselineCommand command, CancellationToken ct = default);

    Task PublishInvalidateCacheAsync(InvalidateCacheCommand command, CancellationToken ct = default);
}
