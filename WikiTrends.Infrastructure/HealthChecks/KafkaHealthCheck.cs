using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using WikiTrends.Infrastructure.Kafka.Settings;

namespace WikiTrends.Infrastructure.HealthChecks;

/// <summary>
/// Health check для проверки подключения к Kafka.
/// Использует AdminClient для получения метаданных кластера.
/// </summary>
public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaSettings _settings;

    public KafkaHealthCheck(IOptions<KafkaSettings> settings)
    {
        // TODO: Сохранить settings.Value в _settings
        _settings = settings.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var acc = new AdminClientConfig { BootstrapServers = _settings.BootstrapServers };
            using var adminClient = new AdminClientBuilder(acc).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            if (metadata.Brokers.Count > 0)
            {
                return HealthCheckResult.Healthy($"Connected to {metadata.Brokers.Count} broker(s)",
                    new Dictionary<string, object>
                    {
                        { "brokers", metadata.Brokers.Select(b => $"{b.Host}:{b.Port}").ToList() },
                        { "originatingBrokerId", metadata.OriginatingBrokerId },
                        { "controllerId", metadata.OriginatingBrokerId }
                    }
                );
            }
            else throw new Exception("Connection established but broker list is empty.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Kafka connection failed", exception, new Dictionary<string, object>
                {
                    { "bootstrapServers", _settings.BootstrapServers },
                    { "errorType", exception.GetType().Name }
                });
        }
    }
}