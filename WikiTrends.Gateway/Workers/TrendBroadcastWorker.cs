using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Gateway.Hubs;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Gateway.Workers;

public sealed class TrendBroadcastWorker : BackgroundService
{
    private readonly IHubContext<TrendHub, ITrendHubClient> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceUrlsOptions _serviceUrls;
    private readonly ILogger<TrendBroadcastWorker> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TrendBroadcastWorker(
        IHubContext<TrendHub, ITrendHubClient> hubContext,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceUrlsOptions> serviceUrls,
        ILogger<TrendBroadcastWorker> logger)
    {
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _serviceUrls = serviceUrls.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: 1. Определить источник уведомлений (Kafka topic или polling Aggregator)
        // TODO: 2. В цикле читать события и формировать TrendNotification
        // TODO: 3. Пушить уведомления в SignalR через _hubContext.Clients.All.ReceiveTrendNotification(...)
        // TODO: 4. Обрабатывать ошибки и продолжать
        _logger.LogInformation("TrendBroadcastWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollAndBroadcastAsync(stoppingToken);
        }
    }

    private async Task PollAndBroadcastAsync(CancellationToken ct)
    {
        try
        {
            var baseUrl = _serviceUrls.AggregatorBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/trends?period={TrendPeriod.Last24Hours}&limit=10&minAnomalyScore=0";

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Trend polling failed. StatusCode={StatusCode}", (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var trends = JsonSerializer.Deserialize<TrendsResponse>(json, _jsonOptions);
            if (trends?.Topics is null)
            {
                return;
            }

            foreach (var topic in trends.Topics.Take(5))
            {
                var notification = new TrendNotification
                {
                    TopicId = topic.TopicId,
                    TopicName = topic.Name,
                    Type = NotificationType.TrendUpdate,
                    AnomalyScore = topic.AnomalyScore,
                    Message = "Trend updated",
                    Timestamp = DateTimeOffset.UtcNow
                };

                await _hubContext.Clients.Group($"topic:{topic.TopicId}").ReceiveTrendNotification(notification);
                await _hubContext.Clients.All.ReceiveTrendNotification(notification);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Trend polling failed: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trend broadcasting iteration failed.");
        }
    }
}
