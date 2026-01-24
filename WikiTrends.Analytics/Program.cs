using Serilog;
using Microsoft.AspNetCore.Http;
using WikiTrends.Analytics;
using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Analytics.Services;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Configuration;
using WikiTrends.Infrastructure.Logging;

SerilogExtensions.CreateBootstrapLogger("WikiAnalytics");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog(configuration =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", "WikiAnalytics");
    });

    builder.Services.AddAnalyticsServices(builder.Configuration);

    var app = builder.Build();

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var clickHouseClient = scope.ServiceProvider.GetRequiredService<IClickHouseClient>();
        await clickHouseClient.EnsureSchemaAsync(app.Lifetime.ApplicationStopping);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to ensure ClickHouse schema on startup");
    }

    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

    app.MapGet("/api/trends", async ([AsParameters] GetTrendsRequest request, IClickHouseClient clickHouseClient, IBaselineService baselineService, IAnomalyDetectionService anomalyDetectionService, CancellationToken ct) =>
    {
        var raw = await clickHouseClient.QueryTrendsAsync(request.Period, ct);

        var topics = new List<TopicTrendDto>();

        var topicInfo = await clickHouseClient.GetTopicInfoAsync(raw.Select(t => t.TopicId), ct);

        foreach (var trend in raw
            .OrderByDescending(t => t.EditCount)
            .Take(Math.Max(1, request.Limit)))
        {
            var baseline = await baselineService.GetBaselineAsync(trend.TopicId, ct);
            var anomaly = await anomalyDetectionService.DetectAsync(trend, baseline, ct);

            if (anomaly.AnomalyScore < request.MinAnomalyScore)
            {
                continue;
            }

            var topArticles = await clickHouseClient.GetTopArticlesForTopicAsync(
                trend.TopicId,
                request.Period,
                limit: 10,
                ct);

            topicInfo.TryGetValue(trend.TopicId, out var info);
            var name = string.IsNullOrWhiteSpace(info.Name) ? $"topic-{trend.TopicId}" : info.Name;
            var path = string.IsNullOrWhiteSpace(info.Path) ? name : info.Path;

            topics.Add(new TopicTrendDto
            {
                TopicId = trend.TopicId,
                Name = name,
                Path = path,
                EditCount = trend.EditCount,
                AnomalyScore = anomaly.AnomalyScore,
                ChangePercent = anomaly.ChangePercent,
                Direction = anomaly.ChangePercent switch
                {
                    > 0.05f => TrendDirection.Rising,
                    < -0.05f => TrendDirection.Falling,
                    _ => TrendDirection.Stable
                },
                TopArticles = topArticles.Select(a => new ArticleDto
                {
                    Id = a.ArticleId,
                    WikiPageId = a.ArticleId,
                    Title = a.Title,
                    Wiki = "enwiki",
                    Extract = null,
                    WikiUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(a.Title)}",
                    EditCount = a.EditCount,
                    UniqueEditors = a.UniqueEditors,
                    LastEditAt = DateTimeOffset.UtcNow
                }).ToList()
            });
        }

        return Results.Ok(new TrendsResponse
        {
            Period = request.Period,
            Topics = topics,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    });

    app.MapGet("/api/trends/clusters", async (TrendPeriod period, CancellationToken ct) =>
    {
        await Task.CompletedTask;
        return Results.Ok(new ClusterResponse
        {
            Clusters = Array.Empty<ClusterDto>(),
            GeneratedAt = DateTimeOffset.UtcNow
        });
    });

    app.MapGet("/api/topics/{topicId:int}", async (int topicId, TrendPeriod period, IClickHouseClient clickHouseClient, IBaselineService baselineService, IAnomalyDetectionService anomalyDetectionService, CancellationToken ct) =>
    {
        if (topicId <= 0)
        {
            return Results.BadRequest(new { error = "TopicId must be positive" });
        }

        var baseline = await baselineService.GetBaselineAsync(topicId, ct);

        var topicInfo = await clickHouseClient.GetTopicInfoAsync(new[] { topicId }, ct);
        topicInfo.TryGetValue(topicId, out var info);
        var name = string.IsNullOrWhiteSpace(info.Name) ? $"topic-{topicId}" : info.Name;
        var path = string.IsNullOrWhiteSpace(info.Path) ? name : info.Path;

        var lastHour = (await clickHouseClient.QueryTrendsAsync(TrendPeriod.LastHour, ct)).FirstOrDefault(t => t.TopicId == topicId);
        var last24 = (await clickHouseClient.QueryTrendsAsync(TrendPeriod.Last24Hours, ct)).FirstOrDefault(t => t.TopicId == topicId);
        var last7 = (await clickHouseClient.QueryTrendsAsync(TrendPeriod.Last7Days, ct)).FirstOrDefault(t => t.TopicId == topicId);

        var selected = period switch
        {
            TrendPeriod.LastHour => lastHour,
            TrendPeriod.Last7Days => last7,
            _ => last24
        };

        float anomalyScore = 0;
        if (selected != null)
        {
            var detection = await anomalyDetectionService.DetectAsync(selected, baseline, ct);
            anomalyScore = detection.AnomalyScore;
        }

        var topArticles = await clickHouseClient.GetTopArticlesForTopicAsync(topicId, period, limit: 50, ct);

        return Results.Ok(new TopicDetailResponse
        {
            TopicId = topicId,
            Name = name,
            Path = path,
            Stats = new TopicStatsDto
            {
                EditCountLastHour = lastHour?.EditCount ?? 0,
                EditCountLast24Hours = last24?.EditCount ?? 0,
                EditCountLast7Days = last7?.EditCount ?? 0,
                BaselineDaily = baseline.BaselineDaily,
                AnomalyScore = anomalyScore
            },
            Articles = topArticles.Select(a => new ArticleDto
            {
                Id = a.ArticleId,
                WikiPageId = a.ArticleId,
                Title = a.Title,
                Wiki = "enwiki",
                Extract = null,
                WikiUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(a.Title)}",
                EditCount = a.EditCount,
                UniqueEditors = a.UniqueEditors,
                LastEditAt = DateTimeOffset.UtcNow
            }).ToList(),
            RelatedTopics = Array.Empty<RelatedTopicDto>()
        });
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WikiAnalytics terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
