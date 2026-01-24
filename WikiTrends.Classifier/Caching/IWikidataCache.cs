using WikiTrends.Classifier.Models;

namespace WikiTrends.Classifier.Caching;

public interface IWikidataCache
{
    bool TryGet(string key, out WikidataResponse? value);

    void Set(string key, WikidataResponse value, TimeSpan ttl);

    void Remove(string key);
}
