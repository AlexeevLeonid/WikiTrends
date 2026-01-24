using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Classifier.Services;

public interface IClassificationService
{
    Task<Result<ClassifiedEditEvent>> ClassifyAsync(EnrichedEditEvent editEvent, CancellationToken ct = default);
}
