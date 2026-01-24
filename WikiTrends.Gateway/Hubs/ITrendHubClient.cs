using WikiTrends.Contracts.Api;

namespace WikiTrends.Gateway.Hubs;

public interface ITrendHubClient
{
    Task ReceiveTrendNotification(TrendNotification notification);
}
