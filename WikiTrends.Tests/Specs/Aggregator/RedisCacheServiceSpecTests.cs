using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using WikiTrends.Aggregator.Cache;

namespace WikiTrends.Tests.Specs.Aggregator;

[Trait("Category", "Spec")]
public sealed class RedisCacheServiceSpecTests
{
    [Fact]
    public async Task GetStringAsync_CallsDistributedCache_GetStringAsync()
    {
        var distributed = new FakeDistributedCache();
        await distributed.SetAsync(
            "k",
            Encoding.UTF8.GetBytes("v"),
            new DistributedCacheEntryOptions(),
            CancellationToken.None);

        var svc = new RedisCacheService(distributed, NullLogger<RedisCacheService>.Instance);

        var value = await svc.GetStringAsync("k", CancellationToken.None);
        Assert.Equal("v", value);
        Assert.Equal("k", distributed.LastGetKey);
    }

    [Fact]
    public async Task SetStringAsync_CallsDistributedCache_SetStringAsync_WithAbsoluteExpiration()
    {
        var distributed = new FakeDistributedCache();

        var svc = new RedisCacheService(distributed, NullLogger<RedisCacheService>.Instance);

        var ttl = TimeSpan.FromSeconds(10);
        await svc.SetStringAsync("k", "v", ttl, CancellationToken.None);

        Assert.Equal("k", distributed.LastSetKey);
        Assert.Equal("v", Encoding.UTF8.GetString(distributed.LastSetValueBytes ?? Array.Empty<byte>()));
        Assert.NotNull(distributed.LastSetOptions);
        Assert.Equal(ttl, distributed.LastSetOptions!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task RemoveAsync_CallsDistributedCache_RemoveAsync()
    {
        var distributed = new FakeDistributedCache();
        await distributed.SetAsync(
            "k",
            Encoding.UTF8.GetBytes("v"),
            new DistributedCacheEntryOptions(),
            CancellationToken.None);

        var svc = new RedisCacheService(distributed, NullLogger<RedisCacheService>.Instance);

        await svc.RemoveAsync("k", CancellationToken.None);
        Assert.Equal("k", distributed.LastRemoveKey);
        Assert.Null(await distributed.GetAsync("k", CancellationToken.None));
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, (byte[] Value, DistributedCacheEntryOptions Options)> _storage = new();

        public string? LastGetKey { get; private set; }
        public string? LastSetKey { get; private set; }
        public byte[]? LastSetValueBytes { get; private set; }
        public DistributedCacheEntryOptions? LastSetOptions { get; private set; }
        public string? LastRemoveKey { get; private set; }

        public byte[]? Get(string key)
        {
            _storage.TryGetValue(key, out var entry);
            return entry.Value;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            LastGetKey = key;
            _storage.TryGetValue(key, out var entry);
            return Task.FromResult<byte[]?>(entry.Value);
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _storage.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            LastRemoveKey = key;
            _storage.Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _storage[key] = (value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            LastSetKey = key;
            LastSetValueBytes = value;
            LastSetOptions = options;
            _storage[key] = (value, options);
            return Task.CompletedTask;
        }
    }
}
