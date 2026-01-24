namespace WikiTrends.Aggregator.Cache;

public interface ICacheService
{
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);

    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}
