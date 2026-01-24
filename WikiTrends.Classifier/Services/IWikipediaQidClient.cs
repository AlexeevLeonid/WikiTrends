using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public interface IWikipediaQidClient
{
    Task<Result<string>> GetWikidataIdAsync(string title, string lang, CancellationToken ct = default);
}
