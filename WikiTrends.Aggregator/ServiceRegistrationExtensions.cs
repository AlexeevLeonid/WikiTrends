using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WikiTrends.Aggregator.Cache;
using WikiTrends.Aggregator.Configuration;
using WikiTrends.Aggregator.DataSources;
using WikiTrends.Aggregator.Handlers;
using WikiTrends.Aggregator.Services;
using WikiTrends.Aggregator.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Extensions;

namespace WikiTrends.Aggregator;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddAggregatorServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<AggregatorOptions>(configuration, AggregatorOptions.SectionName);

        services.AddKafkaSettings(configuration);
        services.AddKafkaConsumer<string, TrendUpdateEvent, TrendUpdateHandler>(configuration);
        services.AddKafkaConsumer<string, RecalculateBaselineCommand, CommandHandler>(configuration);
        services.AddKafkaConsumer<string, InvalidateCacheCommand, CommandHandler>(configuration);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        });

        services.AddHttpClient<IAnalyticsDataSource, AnalyticsDataSource>((sp, client) =>
        {
            var urls = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
            if (Uri.TryCreate(urls.AnalyticsBaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddHttpClient<IClassifierDataSource, ClassifierDataSource>((sp, client) =>
        {
            var urls = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
            if (Uri.TryCreate(urls.ClassifierBaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddHttpClient<IEnricherDataSource, EnricherDataSource>((sp, client) =>
        {
            var urls = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
            if (Uri.TryCreate(urls.EnricherBaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<IAggregationService, AggregationService>();

        services.AddHostedService<TrendCacheWorker>();
        services.AddHostedService<TrendUpdateWorker>();
        services.AddHostedService<CommandWorker>();

        return services;
    }
}
