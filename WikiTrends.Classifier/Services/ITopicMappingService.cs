using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using WikiTrends.Classifier.Models;

namespace WikiTrends.Classifier.Services;

public interface ITopicMappingService
{
    Task<Result<IReadOnlyList<TopicScore>>> MapTopicsAsync(EnrichedEditEvent editEvent, WikidataResponse? wikidata, CancellationToken ct = default);
}
