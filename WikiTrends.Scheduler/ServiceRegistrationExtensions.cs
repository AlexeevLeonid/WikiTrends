using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Extensions;
using WikiTrends.Scheduler.Configuration;
using WikiTrends.Scheduler.Jobs;
using WikiTrends.Scheduler.Services;
using WikiTrends.Scheduler.Workers;

namespace WikiTrends.Scheduler;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddSchedulerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<SchedulerOptions>(configuration, SchedulerOptions.SectionName);

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddKafkaSettings(configuration);
        services.AddKafkaProducer<string, RecalculateBaselineCommand>(configuration);
        services.AddKafkaProducer<string, InvalidateCacheCommand>(configuration);

        services.AddScoped<ICommandPublisher, CommandPublisher>();

        services.AddScoped<SystemHealthCheckJob>();
        services.AddScoped<DataCleanupJob>();
        services.AddScoped<BaselineRecalculationJob>();

        services.AddHostedService<BaselineRecalculationWorker>();
        services.AddHostedService<SystemHealthCheckWorker>();

        return services;
    }
}
