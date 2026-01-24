using WikiTrends.Analytics.Models;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Analytics.Services;

public sealed class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> _logger;

    public AnomalyDetectionService(ILogger<AnomalyDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<AnomalyResult> DetectAsync(TrendData trend, BaselineData baseline, CancellationToken ct = default)
    {
        //  1. Провалидировать входные данные trend/baseline
        //  2. Посчитать baselineDaily/expected, z-score/percent change
        //  3. Сформировать AnomalyResult
        //  4. Вернуть результат
        if (trend == null) throw new ArgumentNullException(nameof(trend));
        if (baseline == null) throw new ArgumentNullException(nameof(baseline));
        if (trend.TopicId != baseline.TopicId)
        {
            throw new ArgumentException($"TopicId mismatch: Trend {trend.TopicId} vs Baseline {baseline.TopicId}");
        }

        // Приводим базовое дневное значение к масштабу периода тренда
        double expected = trend.Period switch
        {
            TrendPeriod.LastHour => baseline.BaselineDaily / 24.0,
            TrendPeriod.Last24Hours => baseline.BaselineDaily,
            TrendPeriod.Last7Days => baseline.BaselineDaily * 7.0,
            _ => baseline.BaselineDaily // Fallback
        };

        double actual = trend.EditCount;
        double diff = actual - expected;

        // --- Математика ---
        float percentChange;
        float anomalyScore;

        // Защита от деления на ноль (очень редкий или новый топик)
        // Epsilon для сравнения с плавающей точкой
        const double epsilon = 0.0001;

        if (expected < epsilon)
        {
            if (actual < epsilon)
            {
                // И ждали 0, и пришло 0 -> тишина, не аномалия
                percentChange = 0f;
                anomalyScore = 0f;
            }
            else
            {
                // "Холодным старт" или взрывной рост с нуля.
                // Ждали 0, пришло 5. Математически рост бесконечный.
                // Ставим условные 100% (1.0f) или больше, а Score = абсолютному значению.
                percentChange = 1.0f;
                anomalyScore = (float)actual; // Чем больше пришло событий на пустом месте, тем выше скор
            }
        }
        else
        {
            // Формула: (действительное - ожидаемое) / ожидаемое
            // Пример: Ждали 100, пришло 150 -> (50 / 100) = 0.5 (50%)
            percentChange = (float)(diff / expected);

            // Anomaly Score (Z-Score через Poisson)
            // Формула: (действительное - ожидаемое) / sqrt(ожидаемое)
            // в распределении Пуассона дисперсия = мат. ожиданию.
            // позволяет понять, является ли рост на 5 событий шумом (при ожидании 100)
            // или событием (при ожидании 1).
            double stdDev = Math.Sqrt(expected);
            anomalyScore = (float)(diff / stdDev);
        }

        var result = new AnomalyResult
        {
            AnomalyScore = anomalyScore,
            ChangePercent = percentChange
        };


        return result;
    }
}
