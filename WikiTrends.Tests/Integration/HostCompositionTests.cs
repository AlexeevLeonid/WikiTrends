using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Workers;
using WikiTrends.Aggregator.Configuration;
using WikiTrends.Aggregator.Workers;
using WikiTrends.Classifier.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Workers;
using WikiTrends.Gateway.Hubs;
using WikiTrends.Gateway.Workers;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Tests.Integration;

public sealed class HostCompositionTests
{
    [Fact]
    public void CanBuildServiceProvider_WithAllWorkersRegistered()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpClient();

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new TopicsOptions
        {
            RawEdits = "wiki.raw-edits",
            EnrichedEdits = "wiki.enriched",
            ClassifiedEdits = "wiki.classified",
            TrendUpdates = "wiki.trend-updates",
            RecalculateBaselineCommands = "wiki.commands.recalculate-baseline",
            InvalidateCacheCommands = "wiki.commands.invalidate-cache"
        }));

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions
        {
            TrendCalculationIntervalSeconds = 60,
            TopTopicsLimit = 50,
            TopArticlesPerTopicLimit = 20
        }));

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AggregatorOptions
        {
            TrendCacheRefreshSeconds = 60
        }));

        // Dependencies for workers (mocks)
        services.AddSingleton(Mock.Of<IKafkaConsumer<string, RawEditEvent>>());
        services.AddSingleton(Mock.Of<IKafkaConsumer<string, EnrichedEditEvent>>());
        services.AddSingleton(Mock.Of<IKafkaConsumer<string, ClassifiedEditEvent>>());
        services.AddSingleton(Mock.Of<IKafkaConsumer<string, RecalculateBaselineCommand>>());
        services.AddSingleton(Mock.Of<IKafkaConsumer<string, InvalidateCacheCommand>>());
        services.AddSingleton(Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<TrendHub, ITrendHubClient>>());

        // Workers
        services.AddSingleton<EnricherWorker>();
        services.AddSingleton<ClassifierWorker>();
        services.AddSingleton<EventConsumerWorker>();
        services.AddSingleton<TrendCalculationWorker>();
        services.AddSingleton<TrendCacheWorker>();
        services.AddSingleton<CommandWorker>();
        services.AddSingleton<TrendBroadcastWorker>();

        services.AddSingleton(NullLogger<EnricherWorker>.Instance);
        services.AddSingleton(NullLogger<ClassifierWorker>.Instance);
        services.AddSingleton(NullLogger<EventConsumerWorker>.Instance);
        services.AddSingleton(NullLogger<TrendCalculationWorker>.Instance);
        services.AddSingleton(NullLogger<TrendCacheWorker>.Instance);
        services.AddSingleton(NullLogger<CommandWorker>.Instance);
        services.AddSingleton(NullLogger<TrendBroadcastWorker>.Instance);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Assert creation
        Assert.NotNull(provider.GetRequiredService<EnricherWorker>());
        Assert.NotNull(provider.GetRequiredService<ClassifierWorker>());
        Assert.NotNull(provider.GetRequiredService<EventConsumerWorker>());
        Assert.NotNull(provider.GetRequiredService<TrendCalculationWorker>());
        Assert.NotNull(provider.GetRequiredService<TrendCacheWorker>());
        Assert.NotNull(provider.GetRequiredService<CommandWorker>());
        Assert.NotNull(provider.GetRequiredService<TrendBroadcastWorker>());
    }
}
