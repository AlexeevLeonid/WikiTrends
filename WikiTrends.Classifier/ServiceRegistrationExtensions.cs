using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WikiTrends.Classifier.Caching;
using WikiTrends.Classifier.Configuration;
using WikiTrends.Classifier.Data;
using WikiTrends.Classifier.Data.Repositories;
using WikiTrends.Classifier.Handlers;
using WikiTrends.Classifier.Seed;
using WikiTrends.Classifier.Services;
using WikiTrends.Classifier.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Extensions;
using WikiTrends.Infrastructure.Persistence.Extensions;

namespace WikiTrends.Classifier;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddClassifierServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<ClassifierOptions>(configuration, ClassifierOptions.SectionName);

        services.AddKafkaSettings(configuration);
        services.AddKafkaConsumer<string, EnrichedEditEvent, EnrichedEditHandler>(configuration);
        services.AddKafkaProducer<string, ClassifiedEditEvent>(configuration);

        services.AddPostgresDbContext<ClassifierDbContext>(configuration);

        services.AddHttpClient<IWikidataClient, WikidataClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Add("Api-User-Agent", "WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHttpClient<IWikipediaQidClient, WikipediaQidClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHttpClient<IWikidataSparqlClient, WikidataSparqlClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ClassifierOptions>>().Value;
            var timeoutSeconds = Math.Clamp(options.SparqlTimeoutSeconds, 1, 60);

            client.BaseAddress = new Uri("https://query.wikidata.org/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiTrends/1.0 (local dev; +https://github.com/wikitrends)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/sparql-results+json");
        });

        services.AddMemoryCache();
        services.AddSingleton<IWikidataCache, WikidataMemoryCache>();

        services.AddScoped<IClassificationService, ClassificationService>();
        services.AddScoped<ITopicResolverService, TopicResolverService>();

        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<IArticleTopicRepository, ArticleTopicRepository>();
        services.AddScoped<IWikidataMappingRepository, WikidataMappingRepository>();
        services.AddScoped<TopicSeeder>();

        var workerCount = configuration.GetValue<int>($"{ClassifierOptions.SectionName}:WorkerCount", 1);
        for (var i = 0; i < workerCount; i++)
        {
            services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<ClassifierWorker>(sp));
        }

        return services;
    }
}
