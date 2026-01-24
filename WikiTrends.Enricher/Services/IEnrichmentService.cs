using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Enricher.Services;

public interface IEnrichmentService
{
    Task<Result<EnrichedEditEvent>> EnrichAsync(RawEditEvent editEvent, CancellationToken ct = default);
}
