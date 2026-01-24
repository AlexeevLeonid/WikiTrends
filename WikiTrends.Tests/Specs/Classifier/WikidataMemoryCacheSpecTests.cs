using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WikiTrends.Classifier.Caching;
using WikiTrends.Classifier.Models;

namespace WikiTrends.Tests.Specs.Classifier;

[Trait("Category", "Spec")]
public sealed class WikidataMemoryCacheSpecTests
{
    [Fact]
    public void Set_ThenTryGet_ReturnsCachedValue()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new WikidataMemoryCache(memory, NullLogger<WikidataMemoryCache>.Instance);

        var value = new WikidataResponse { Entity = null, Claims = Array.Empty<string>() };

        cache.Set("k", value, TimeSpan.FromSeconds(10));

        var ok = cache.TryGet("k", out var got);
        Assert.True(ok);
        Assert.NotNull(got);
    }

    [Fact]
    public void Remove_DeletesKey()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new WikidataMemoryCache(memory, NullLogger<WikidataMemoryCache>.Instance);

        var value = new WikidataResponse { Entity = null, Claims = Array.Empty<string>() };
        cache.Set("k", value, TimeSpan.FromSeconds(10));

        cache.Remove("k");

        var ok = cache.TryGet("k", out _);
        Assert.False(ok);
    }
}
