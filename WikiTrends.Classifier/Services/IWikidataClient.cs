using WikiTrends.Contracts.Common;
using WikiTrends.Classifier.Models;

namespace WikiTrends.Classifier.Services;

public interface IWikidataClient
{
    Task<Result<WikidataResponse>> GetEntityAsync(string title, string wiki, CancellationToken ct = default);

    Task<Result<IReadOnlyDictionary<string, string>>> GetLabelsAsync(IEnumerable<string> entityIds, string wiki, CancellationToken ct = default);
}
