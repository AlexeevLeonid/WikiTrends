using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Confluent.Kafka;
using WikiTrends.Aggregator;
using WikiTrends.Aggregator.Workers;
using WikiTrends.Analytics;
using WikiTrends.Analytics.Workers;
using WikiTrends.Classifier;
using WikiTrends.Classifier.Data;
using WikiTrends.Classifier.Workers;
using WikiTrends.Collector;
using WikiTrends.Collector.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher;
using WikiTrends.Enricher.Data;
using WikiTrends.Enricher.Workers;
using WikiTrends.Gateway;
using WikiTrends.Gateway.Workers;
using WikiTrends.Infrastructure.Kafka.Consumer;
using WikiTrends.Infrastructure.Kafka.Producer;
using WikiTrends.Scheduler;

namespace WikiTrends.Tests.Integration;

public sealed class ServiceStartupIntegrationTests
{
    [Fact]
    public async Task Scheduler_WebApp_StartsAndStops_WithMocks()
    {
        var config = CreateConfiguration();

        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddSchedulerServices(builder.Configuration);

        OverrideKafkaForScheduler(builder.Services);

        await using var app = builder.Build();

        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task Gateway_WebApp_StartsAndStops_WithMocks_AndWithoutBroadcastWorker()
    {
        var config = CreateConfiguration();

        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddGatewayServices(builder.Configuration);

        RemoveHostedService<TrendBroadcastWorker>(builder.Services);

        await using var app = builder.Build();

        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task Enricher_Host_StartsAndStops_WithKafkaMocks_AndEfInMemory()
    {
        var config = CreateConfiguration();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddEnricherServices(builder.Configuration);

        UseInMemoryDbContext<EnricherDbContext>(builder.Services, "Enricher");
        OverrideKafkaForEnricher(builder.Services);

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Collector_Host_StartsAndStops_WithKafkaMocks_AndWithoutStreamWorker()
    {
        var config = CreateConfiguration();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddCollectorServices(builder.Configuration);

        RemoveHostedService<WikiStreamWorker>(builder.Services);

        OverrideKafkaForCollector(builder.Services);

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Classifier_Host_StartsAndStops_WithKafkaMocks_AndEfInMemory_AndWithoutWorker()
    {
        var config = CreateConfiguration();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddClassifierServices(builder.Configuration);

        RemoveHostedService<ClassifierWorker>(builder.Services);

        UseInMemoryDbContext<ClassifierDbContext>(builder.Services, "Classifier");
        OverrideKafkaForClassifier(builder.Services);

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Analytics_Host_StartsAndStops_WithKafkaMocks_AndWithoutWorkers()
    {
        var config = CreateConfiguration();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddAnalyticsServices(builder.Configuration);

        RemoveHostedService<EventConsumerWorker>(builder.Services);
        RemoveHostedService<TrendCalculationWorker>(builder.Services);

        OverrideKafkaForAnalytics(builder.Services);

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Aggregator_Host_StartsAndStops_WithKafkaMocks_AndWithoutWorkers()
    {
        var config = CreateConfiguration();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        builder.Services.AddAggregatorServices(builder.Configuration);

        RemoveHostedService<TrendCacheWorker>(builder.Services);
        RemoveHostedService<CommandWorker>(builder.Services);

        OverrideKafkaForAggregator(builder.Services);
        OverrideRedis(builder.Services);

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    private static IConfigurationRoot CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:GroupId"] = "wikitrends-tests",
            ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=wikitrends;Username=test;Password=test",
            ["ConnectionStrings:Redis"] = "localhost:6379"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void RemoveHostedService<THosted>(IServiceCollection services)
        where THosted : class, IHostedService
    {
        var toRemove = services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(THosted))
            .ToList();

        foreach (var d in toRemove)
            services.Remove(d);
    }

    private static void UseInMemoryDbContext<TContext>(IServiceCollection services, string dbName)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();

        services.AddDbContext<TContext>(o => o.UseInMemoryDatabase(dbName));
    }

    private static void OverrideKafkaForCollector(IServiceCollection services)
    {
        services.RemoveAll<IKafkaProducer<string, RawEditEvent>>();
        services.AddSingleton<IKafkaProducer<string, RawEditEvent>, FakeKafkaProducer<string, RawEditEvent>>();
    }

    private static void OverrideKafkaForEnricher(IServiceCollection services)
    {
        services.RemoveAll<IKafkaConsumer<string, RawEditEvent>>();
        services.RemoveAll<IKafkaProducer<string, EnrichedEditEvent>>();
        services.AddSingleton<IKafkaConsumer<string, RawEditEvent>, FakeKafkaConsumer<string, RawEditEvent>>();
        services.AddSingleton<IKafkaProducer<string, EnrichedEditEvent>, FakeKafkaProducer<string, EnrichedEditEvent>>();
    }

    private static void OverrideKafkaForClassifier(IServiceCollection services)
    {
        services.RemoveAll<IKafkaConsumer<string, EnrichedEditEvent>>();
        services.RemoveAll<IKafkaProducer<string, ClassifiedEditEvent>>();
        services.AddSingleton<IKafkaConsumer<string, EnrichedEditEvent>, FakeKafkaConsumer<string, EnrichedEditEvent>>();
        services.AddSingleton<IKafkaProducer<string, ClassifiedEditEvent>, FakeKafkaProducer<string, ClassifiedEditEvent>>();
    }

    private static void OverrideKafkaForAnalytics(IServiceCollection services)
    {
        services.RemoveAll<IKafkaConsumer<string, ClassifiedEditEvent>>();
        services.RemoveAll<IKafkaProducer<string, TrendUpdateEvent>>();
        services.AddSingleton<IKafkaConsumer<string, ClassifiedEditEvent>, FakeKafkaConsumer<string, ClassifiedEditEvent>>();
        services.AddSingleton<IKafkaProducer<string, TrendUpdateEvent>, FakeKafkaProducer<string, TrendUpdateEvent>>();
    }

    private static void OverrideKafkaForAggregator(IServiceCollection services)
    {
        services.RemoveAll<IKafkaConsumer<string, TrendUpdateEvent>>();
        services.RemoveAll<IKafkaConsumer<string, RecalculateBaselineCommand>>();
        services.RemoveAll<IKafkaConsumer<string, InvalidateCacheCommand>>();
        services.AddSingleton<IKafkaConsumer<string, TrendUpdateEvent>, FakeKafkaConsumer<string, TrendUpdateEvent>>();
        services.AddSingleton<IKafkaConsumer<string, RecalculateBaselineCommand>, FakeKafkaConsumer<string, RecalculateBaselineCommand>>();
        services.AddSingleton<IKafkaConsumer<string, InvalidateCacheCommand>, FakeKafkaConsumer<string, InvalidateCacheCommand>>();
    }

    private static void OverrideKafkaForScheduler(IServiceCollection services)
    {
        services.RemoveAll<IKafkaProducer<string, RecalculateBaselineCommand>>();
        services.RemoveAll<IKafkaProducer<string, InvalidateCacheCommand>>();
        services.AddSingleton<IKafkaProducer<string, RecalculateBaselineCommand>, FakeKafkaProducer<string, RecalculateBaselineCommand>>();
        services.AddSingleton<IKafkaProducer<string, InvalidateCacheCommand>, FakeKafkaProducer<string, InvalidateCacheCommand>>();
    }

    private static void OverrideRedis(IServiceCollection services)
    {
        services.RemoveAll<IDistributedCache>();
        services.AddSingleton<IDistributedCache>(_ => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
    }

    private sealed class FakeKafkaConsumer<TKey, TValue> : IKafkaConsumer<TKey, TValue>
    {
        public Task StartAsync(string topic, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeKafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>
    {
        public Task<DeliveryResult<TKey, TValue>> ProduceAsync(
            string topic,
            TKey key,
            TValue value,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryResult<TKey, TValue> { Topic = topic });

        public Task<DeliveryResult<TKey, TValue>> ProduceAsync(
            string topic,
            TKey key,
            TValue value,
            Headers? headers,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryResult<TKey, TValue> { Topic = topic });

        public void Flush(TimeSpan timeout)
        {
        }

        public void Dispose()
        {
        }
    }
}
