using WikiTrends.Analytics.Models;

namespace WikiTrends.Analytics.Services;

public interface IBaselineService
{
    Task<BaselineData> GetBaselineAsync(int topicId, CancellationToken ct = default);

    Task RecalculateBaselinesAsync(CancellationToken ct = default);
}
