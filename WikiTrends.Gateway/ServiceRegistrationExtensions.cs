using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WikiTrends.Gateway.Configuration;
using WikiTrends.Gateway.Hubs;
using WikiTrends.Gateway.Services;
using WikiTrends.Gateway.Workers;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Gateway;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<TopicsOptions>(configuration, TopicsOptions.SectionName);
        services.AddValidatedOptions<ServiceUrlsOptions>(configuration, ServiceUrlsOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<GatewayOptions>(configuration, GatewayOptions.SectionName);

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddSignalR();

        services.AddHttpClient();
        services.AddScoped<ITrendService, TrendService>();
        services.AddScoped<ITopicService, TopicService>();

        services.AddHostedService<TrendBroadcastWorker>();

        return services;
    }
}
