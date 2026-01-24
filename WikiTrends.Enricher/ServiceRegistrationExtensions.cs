using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WikiTrends.Contracts.Events;
using WikiTrends.Enricher.Configuration;
using WikiTrends.Enricher.Data;
using WikiTrends.Enricher.Data.Repositories;
using WikiTrends.Enricher.Handlers;
using WikiTrends.Enricher.Services;
using WikiTrends.Enricher.Workers;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Extensions;
using WikiTrends.Infrastructure.Persistence.Extensions;

namespace WikiTrends.Enricher;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddEnricherServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<EnricherOptions>(configuration, EnricherOptions.SectionName);

        services.AddKafkaSettings(configuration);
        services.AddKafkaConsumer<string, RawEditEvent, RawEditHandler>(configuration);
        services.AddKafkaProducer<string, EnrichedEditEvent>(configuration);

        services.AddPostgresDbContext<EnricherDbContext>(configuration);

        services.AddHttpClient<IWikipediaApiClient, WikipediaApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Add("Api-User-Agent", "WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<IArticleRepository, ArticleRepository>();

        var workerCount = configuration.GetValue<int>($"{EnricherOptions.SectionName}:WorkerCount", 1);
        for (var i = 0; i < workerCount; i++)
        {
            services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<EnricherWorker>(sp));
        }

        return services;
    }
}
