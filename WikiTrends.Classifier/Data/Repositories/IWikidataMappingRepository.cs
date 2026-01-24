using WikiTrends.Classifier.Data.Entities;

namespace WikiTrends.Classifier.Data.Repositories;

public interface IWikidataMappingRepository
{
    Task<WikidataMappingEntity?> GetAsync(string wiki, string title, CancellationToken ct = default);

    Task<WikidataMappingEntity> UpsertAsync(string wiki, string title, string? wikidataId, CancellationToken ct = default);
}
