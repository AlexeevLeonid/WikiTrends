using WikiTrends.Analytics.ClickHouse;
using WikiTrends.Analytics.Configuration;
using WikiTrends.Analytics.Models;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;
using Microsoft.Extensions.Options;

namespace WikiTrends.Analytics.Services;

public sealed class BaselineService : IBaselineService
{
    private readonly IClickHouseClient _clickHouseClient;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<BaselineService> _logger;

    public BaselineService(
        IClickHouseClient clickHouseClient,
        IOptions<AnalyticsOptions> options,
        ILogger<BaselineService> logger)
    {
        _clickHouseClient = clickHouseClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BaselineData> GetBaselineAsync(int topicId, CancellationToken ct = default)
    {
        //  1. Провалидировать topicId
        //  2. Загрузить baseline из ClickHouse (или кэша), если отсутствует — создать дефолт
        //  3. Вернуть BaselineData
        if (topicId <= 0)
        {
            _logger.LogError("Topic id <0 {topicId}", topicId);
            throw new ArgumentOutOfRangeException();
        }
        try
        {
            //  2. Загрузка из ClickHouse
            // Метод клиента должен использовать FINAL, чтобы получить актуальную версию
            var baseline = await _clickHouseClient.GetBaselineAsync(topicId, ct);

            if (baseline == null)
            {
                return new BaselineData
                {
                    TopicId = topicId,
                    BaselineDaily = 0.0f,
                    CalculatedAt = DateTime.UtcNow
                };
            }

            return baseline;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching baseline for topic {TopicId}", topicId);
            throw;
        }
    }

    public async Task RecalculateBaselinesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting baselines recalculation...");

        try
        {
            var newBaselines = await _clickHouseClient.ComputeBaselinesFromHistoryAsync(
                daysToLookBack: Math.Max(1, _options.BaselineLookbackDays),
                ct);

            if (newBaselines.Count == 0)
            {
                _logger.LogWarning("No historical data found to calculate baselines.");
                return;
            }

            await _clickHouseClient.BulkUpsertBaselinesAsync(newBaselines, ct);

            _logger.LogInformation("Recalculation finished. Updated baselines for {Count} topics.", newBaselines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate baselines");
            throw;
        }
    }
}
