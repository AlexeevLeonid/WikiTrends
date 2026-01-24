namespace WikiTrends.Infrastructure.Kafka.Consumer;

/// <summary>
/// Обработчик сообщений из Kafka.
/// Реализуется в каждом сервисе для своей бизнес-логики.
/// </summary>
/// <typeparam name="TValue">Тип сообщения</typeparam>
public interface IMessageHandler<in TValue>
{
    /// <summary>
    /// Обрабатывает полученное сообщение
    /// </summary>
    /// <param name="message">Десериализованное сообщение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Task, завершающийся после обработки</returns>
    Task HandleAsync(TValue message, CancellationToken cancellationToken);
}