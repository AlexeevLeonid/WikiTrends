using Microsoft.Extensions.DependencyInjection;

namespace WikiTrends.Infrastructure.HealthChecks.Extensions;

/// <summary>
/// Extension methods для регистрации health checks
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет Kafka health check
    /// </summary>
    /// <param name="name">Имя health check (по умолчанию "kafka")</param>
    /// <param name="tags">Теги для фильтрации</param>
    public static IServiceCollection AddKafkaHealthCheck(
        this IServiceCollection services,
        string name = "kafka",
        params string[] tags)
    {
        services.AddHealthChecks()
                   .AddCheck<KafkaHealthCheck>(name, tags: tags);
        return services;
    }
}