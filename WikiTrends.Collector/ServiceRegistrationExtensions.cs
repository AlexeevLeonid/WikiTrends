using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WikiTrends.Collector.Mapping;
using WikiTrends.Collector.Services;
using WikiTrends.Collector.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.HealthChecks.Extensions;
using WikiTrends.Infrastructure.Kafka.Extensions;

namespace WikiTrends.Collector;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddCollectorServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<WikiStreamOptions>(configuration, WikiStreamOptions.SectionName);

        services.AddKafkaSettings(configuration);
        services.AddKafkaProducer<string, RawEditEvent>(configuration);

        services.AddHttpClient<IWikiStreamClient, WikiStreamClient>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Add("Accept", "text/event-stream");
            client.DefaultRequestHeaders.Add("User-Agent", "WikiTrends/1.0 (https://github.com/wikitrends)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true
        });

        services.AddSingleton<IEditMapper, EditMapper>();

        services.AddHostedService<WikiStreamWorker>();

        services.AddKafkaHealthCheck();

        return services;
    }
}
