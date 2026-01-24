using Serilog;
using Microsoft.AspNetCore.Mvc;
using WikiTrends.Aggregator;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Logging;
using WikiTrends.Aggregator.Services;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;

SerilogExtensions.CreateBootstrapLogger("WikiAggregator");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logging
    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiAggregator");
    });

    builder.Services.AddAggregatorServices(builder.Configuration);

    var app = builder.Build();

    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

    app.MapGet("/api/trends", async ([AsParameters] GetTrendsRequest request, IAggregationService service, CancellationToken ct) =>
    {
        var result = await service.GetTrendsAsync(request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error);
    });

    app.MapGet("/api/trends/clusters", async (TrendPeriod period, IAggregationService service, CancellationToken ct) =>
    {
        var result = await service.GetClustersAsync(period, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error);
    });

    app.MapGet("/api/topics/{topicId:int}", async (int topicId, TrendPeriod period, IAggregationService service, CancellationToken ct) =>
    {
        var result = await service.GetTopicDetailsAsync(topicId, period, ct);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var fallback = new TopicDetailResponse
        {
            TopicId = topicId,
            Name = $"topic-{topicId}",
            Path = $"topic-{topicId}",
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

        return Results.Ok(fallback);
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiAggregator terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
