using Microsoft.Extensions.Caching.Distributed;

namespace WikiTrends.Aggregator.Cache;

public sealed class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        // TODO: 1. Провалидировать key
        // TODO: 2. Прочитать строку из Redis через _cache.GetStringAsync
        // TODO: 3. Вернуть строку или null
        if (string.IsNullOrWhiteSpace(key)) return null;
        var result = await _cache.GetStringAsync(key, ct);
        return result;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        // TODO: 1. Провалидировать key/value/ttl
        // TODO: 2. Записать строку через _cache.SetStringAsync c DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow
        // TODO: 3. Логировать на Debug
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError("Key is null or empty");
            throw new ArgumentException("Key is null or empty");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogError("value is null or empty");
            throw new ArgumentException("value is null or empty");
        }

        if (ttl == TimeSpan.Zero)
        {
            _logger.LogError("ttl is zero");
            throw new ArgumentException("ttl is zero");
        }
        try
        {
            await _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            _logger.LogDebug("New cache entry: {key} {value} {ttl}", key, value, ttl.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while caching: {key} {value} {Error}", key, value, ex.Message);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        // TODO: 1. Провалидировать key
        // TODO: 2. Удалить ключ из Redis через _cache.RemoveAsync
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogDebug("Removed cache entry: {key} ", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while romoving cache entry: {key} {Error}", key, ex.Message);
        }
    }
}
