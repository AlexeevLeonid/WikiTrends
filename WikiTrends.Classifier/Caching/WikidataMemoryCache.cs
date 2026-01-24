using Microsoft.Extensions.Caching.Memory;
using WikiTrends.Classifier.Models;

namespace WikiTrends.Classifier.Caching;

public sealed class WikidataMemoryCache : IWikidataCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<WikidataMemoryCache> _logger;

    public WikidataMemoryCache(
        IMemoryCache cache,
        ILogger<WikidataMemoryCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool TryGet(string key, out WikidataResponse? value)
    {
        //  1. Провалидировать key
        //  2. Попробовать достать значение из _cache
        //  3. Вернуть true/false
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        var result = _cache.TryGetValue(key, out value);
        return result;
    }

    public void Set(string key, WikidataResponse value, TimeSpan ttl)
    {
        //  1. Провалидировать key/value/ttl
        //  2. Сохранить значение в _cache с абсолютным временем жизни ttl
        //  3. Логировать на Debug уровне
        if (string.IsNullOrEmpty(key)) return;
        _cache.Set(key, value, ttl);
    }

    public void Remove(string key)
    {
        //  1. Провалидировать key
        //  2. Удалить ключ из _cache
        if (string.IsNullOrEmpty(key)) return;
        _cache.Remove(key);
    }
}
