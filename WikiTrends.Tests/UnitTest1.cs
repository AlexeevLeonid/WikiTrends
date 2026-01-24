using System.Text;
using Confluent.Kafka;
using WikiTrends.Infrastructure.Kafka.Serialization;

namespace WikiTrends.Tests;

public sealed class KafkaJsonSerializationTests
{
    private sealed record Sample
    {
        public int SomeValue { get; init; }

        public string? Optional { get; init; }
    }

    [Fact]
    public void KafkaJsonSerializer_SerializesCamelCase_AndIgnoresNulls()
    {
        var serializer = new KafkaJsonSerializer<Sample>();

        var data = new Sample { SomeValue = 1, Optional = null };

        var bytes = serializer.Serialize(data, new SerializationContext(MessageComponentType.Value, "t"));
        Assert.NotNull(bytes);

        var json = Encoding.UTF8.GetString(bytes!);

        Assert.Contains("\"someValue\"", json);
        Assert.DoesNotContain("Optional", json);
        Assert.DoesNotContain("optional", json);
    }

    [Fact]
    public void KafkaJsonDeserializer_DeserializesCaseInsensitive()
    {
        var deserializer = new KafkaJsonDeserializer<Sample>();

        var json = "{\"SomeValue\":2}";
        var bytes = Encoding.UTF8.GetBytes(json);

        var data = deserializer.Deserialize(bytes, false, new SerializationContext(MessageComponentType.Value, "t"));

        Assert.NotNull(data);
        Assert.Equal(2, data.SomeValue);
    }
}