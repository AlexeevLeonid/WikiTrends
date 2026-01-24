using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Gateway.Services;
using WikiTrends.Infrastructure.Configuration;

namespace WikiTrends.Tests.Specs.Gateway;

[Trait("Category", "Spec")]
public sealed class GatewayServiceSpecTests
{
    [Fact]
    public async Task TrendService_GetTrendsAsync_UsesAggregatorBaseUrl_AndDoesNotThrow()
    {
        var response = new TrendsResponse
        {
            Period = TrendPeriod.Last24Hours,
            Topics = Array.Empty<TopicTrendDto>(),
            GeneratedAt = DateTimeOffset.UtcNow
        };
        var http = new HttpClient(new StubHandler(_ => Json(response))) { BaseAddress = new Uri("http://ignored") };

        var service = new TrendService(
            new SingleHttpClientFactory(http),
            Options.Create(new ServiceUrlsOptions { AggregatorBaseUrl = "http://agg" }),
            NullLogger<TrendService>.Instance);

        var ex = await Record.ExceptionAsync(() => service.GetTrendsAsync(new GetTrendsRequest(), CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task TopicService_GetTopicDetailsAsync_UsesAggregatorBaseUrl_AndDoesNotThrow()
    {
        var response = new TopicDetailResponse
        {
            TopicId = 1,
            Name = "t",
            Path = "Root/t",
            Stats = new TopicStatsDto
            {
                EditCountLastHour = 0,
                EditCountLast24Hours = 0,
                EditCountLast7Days = 0,
                BaselineDaily = 0,
                AnomalyScore = 0
            },
            Articles = Array.Empty<ArticleDto>(),
            RelatedTopics = Array.Empty<RelatedTopicDto>()
        };
        var http = new HttpClient(new StubHandler(_ => Json(response))) { BaseAddress = new Uri("http://ignored") };

        var service = new TopicService(
            new SingleHttpClientFactory(http),
            Options.Create(new ServiceUrlsOptions { AggregatorBaseUrl = "http://agg" }),
            NullLogger<TopicService>.Instance);

        var ex = await Record.ExceptionAsync(() => service.GetTopicDetailsAsync(1, TrendPeriod.Last24Hours, CancellationToken.None));
        Assert.Null(ex);
    }

    private static HttpResponseMessage Json<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleHttpClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) => _handle = handle;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }
}
