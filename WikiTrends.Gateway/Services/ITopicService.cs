using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Common;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Gateway.Services;

public interface ITopicService
{
    Task<Result<TopicDetailResponse>> GetTopicDetailsAsync(int topicId, TrendPeriod period, CancellationToken ct = default);
}
