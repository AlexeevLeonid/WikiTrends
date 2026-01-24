using Confluent.Kafka;

namespace WikiTrends.Infrastructure.Kafka.Producer;

/// <summary>
/// Интерфейс для отправки сообщений в Kafka.
/// Абстрагирует работу с Confluent.Kafka Producer.
/// </summary>
/// <typeparam name="TKey">Тип ключа сообщения (обычно string)</typeparam>
/// <typeparam name="TValue">Тип значения сообщения (DTO)</typeparam>
public interface IKafkaProducer<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Асинхронно отправляет сообщение в указанный топик
    /// </summary>
    /// <param name="topic">Имя топика Kafka</param>
    /// <param name="key">Ключ сообщения для партиционирования</param>
    /// <param name="value">Тело сообщения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат доставки с информацией о partition и offset</returns>
    Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно отправляет сообщение в указанный топик с заголовками
    /// </summary>
    Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        Headers headers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно дожидается отправки всех буферизированных сообщений
    /// </summary>
    /// <param name="timeout">Максимальное время ожидания</param>
    void Flush(TimeSpan timeout);
}