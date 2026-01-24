using WikiTrends.Contracts.Common;
using WikiTrends.Enricher.Models;

namespace WikiTrends.Enricher.Services;

public interface IWikipediaApiClient
{
    Task<Result<WikipediaApiResponse>> GetPageDataAsync(string title, string wiki, CancellationToken ct = default);
}
