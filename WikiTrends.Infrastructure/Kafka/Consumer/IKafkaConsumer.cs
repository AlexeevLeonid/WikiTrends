namespace WikiTrends.Infrastructure.Kafka.Consumer;

/// <summary>
/// Интерфейс для потребления сообщений из Kafka.
/// Реализует background processing pattern.
/// </summary>
/// <typeparam name="TKey">Тип ключа</typeparam>
/// <typeparam name="TValue">Тип сообщения</typeparam>
public interface IKafkaConsumer<TKey, TValue>
{
    /// <summary>
    /// Запускает потребление сообщений из указанного топика
    /// </summary>
    /// <param name="topic">Имя топика для подписки</param>
    /// <param name="cancellationToken">Токен для graceful shutdown</param>
    Task StartAsync(string topic, CancellationToken cancellationToken);

    /// <summary>
    /// Останавливает потребление сообщений
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}