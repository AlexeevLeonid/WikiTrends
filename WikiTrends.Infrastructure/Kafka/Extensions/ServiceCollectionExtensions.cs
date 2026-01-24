using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Kafka.Consumer;
using WikiTrends.Infrastructure.Kafka.Producer;
using WikiTrends.Infrastructure.Kafka.Settings;

namespace WikiTrends.Infrastructure.Kafka.Extensions;

/// <summary>
/// Extension methods для регистрации Kafka сервисов в DI
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует KafkaSettings из конфигурации
    /// </summary>
    public static IServiceCollection AddKafkaSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<KafkaSettings>(configuration, KafkaSettings.SectionName);
        return services;
        
    }

    /// <summary>
    /// Регистрирует Kafka Producer как Singleton
    /// </summary>
    /// <typeparam name="TKey">Тип ключа (обычно string)</typeparam>
    /// <typeparam name="TValue">Тип сообщения</typeparam>
    public static IServiceCollection AddKafkaProducer<TKey, TValue>(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<KafkaSettings>(configuration, KafkaSettings.SectionName);
        services.TryAddSingleton<IKafkaProducer<TKey, TValue>, KafkaProducer<TKey, TValue>>();
        return services;
    }

    /// <summary>
    /// Регистрирует Kafka Consumer и его обработчик
    /// </summary>
    /// <typeparam name="TKey">Тип ключа</typeparam>
    /// <typeparam name="TValue">Тип сообщения</typeparam>
    /// <typeparam name="THandler">Тип обработчика сообщений</typeparam>
    public static IServiceCollection AddKafkaConsumer<TKey, TValue, THandler>(
        this IServiceCollection services,
        IConfiguration configuration)
        where THandler : class, IMessageHandler<TValue>
    {

        services.AddValidatedOptions<KafkaSettings>(configuration, KafkaSettings.SectionName);
        services.TryAddSingleton<IMessageHandler<TValue>, THandler>();
        services.TryAddTransient<IKafkaConsumer<TKey, TValue>, KafkaConsumer<TKey, TValue>>();
        return services;
    }
}