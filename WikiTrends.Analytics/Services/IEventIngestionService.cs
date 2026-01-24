using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.Services;

public interface IEventIngestionService
{
    Task IngestAsync(ClassifiedEditEvent editEvent, CancellationToken ct = default);
}
