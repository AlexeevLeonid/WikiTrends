using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Handlers;
using WikiTrends.Analytics.Services;
using WikiTrends.Analytics.Workers;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Extensions;

namespace WikiTrends.Analytics;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddAnalyticsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<AnalyticsOptions>(configuration, AnalyticsOptions.SectionName);
        services.AddValidatedOptions<ClickHouseSettings>(configuration, ClickHouseSettings.SectionName);

        services.AddKafkaSettings(configuration);
        services.AddKafkaConsumer<string, ClassifiedEditEvent, ClassifiedEditHandler>(configuration);
        services.AddKafkaProducer<string, TrendUpdateEvent>(configuration);

        services.AddHttpClient<IClickHouseClient, ClickHouseClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<ClickHouseSettings>>().Value;
            if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddScoped<IEventIngestionService, EventIngestionService>();
        services.AddScoped<ITrendCalculationService, TrendCalculationService>();
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddScoped<IBaselineService, BaselineService>();

        services.AddHostedService<EventConsumerWorker>();
        services.AddHostedService<TrendCalculationWorker>();

        return services;
    }
}
