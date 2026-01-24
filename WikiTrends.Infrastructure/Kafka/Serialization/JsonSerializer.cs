using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;

namespace WikiTrends.Infrastructure.Kafka.Serialization;

/// <summary>
/// JSON сериализатор для Kafka Producer.
/// Конвертирует объекты .NET в байты для отправки в Kafka.
/// </summary>
/// <typeparam name="T">Тип сериализуемого объекта</typeparam>
public sealed class KafkaJsonSerializer<T> : ISerializer<T>
{
    private readonly JsonSerializerOptions _options;

    public KafkaJsonSerializer() : this(null)
    {
    }

    public KafkaJsonSerializer(JsonSerializerOptions? options)
    {
        if (options == null)
        {
            _options = new JsonSerializerOptions();
            _options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            _options.WriteIndented = false;
            _options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        }
        else
            _options = options;
    }

    /// <summary>
    /// Сериализует объект в массив байтов UTF-8
    /// </summary>
    public byte[] Serialize(T data, SerializationContext context)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToUtf8Bytes(data, _options);
    }
}