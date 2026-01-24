using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace WikiTrends.Infrastructure.Kafka.Serialization;

/// <summary>
/// JSON десериализатор для Kafka Consumer.
/// Конвертирует байты из Kafka в объекты .NET.
/// </summary>
/// <typeparam name="T">Тип десериализуемого объекта</typeparam>
public sealed class KafkaJsonDeserializer<T> : IDeserializer<T>
{
    private readonly JsonSerializerOptions _options;

    public KafkaJsonDeserializer() : this(null)
    {
    }

    public KafkaJsonDeserializer(JsonSerializerOptions? options)
    {
        if (options == null)
        {
            _options = new JsonSerializerOptions();
            _options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            _options.PropertyNameCaseInsensitive = true;
        }
        else 
            _options = options;
    }

    /// <summary>
    /// Десериализует массив байтов в объект
    /// </summary>
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull || data.IsEmpty)
            return default!;
        var res = JsonSerializer.Deserialize<T>(data, _options);
        if (res == null) 
            return default!;
        return res;
    }
}