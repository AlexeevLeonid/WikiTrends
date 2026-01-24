using Microsoft.AspNetCore.SignalR;

namespace WikiTrends.Gateway.Hubs;

public sealed class TrendHub : Hub<ITrendHubClient>
{
    private readonly ILogger<TrendHub> _logger;

    public TrendHub(ILogger<TrendHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // TODO: 1. Залогировать подключение клиента (ConnectionId)
        // TODO: 2. (опционально) добавить в группу по умолчанию
        _logger.LogInformation("TrendHub client connected. ConnectionId={ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // TODO: 1. Залогировать отключение клиента
        // TODO: 2. (опционально) удалить из групп
        _logger.LogInformation(
            exception,
            "TrendHub client disconnected. ConnectionId={ConnectionId}",
            Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToTopic(int topicId)
    {
        // TODO: 1. Добавить ConnectionId в группу по topicId
        // TODO: 2. Подтвердить подписку (опционально)
        if (topicId <= 0)
        {
            _logger.LogWarning("Invalid topicId for subscription. ConnectionId={ConnectionId}. TopicId={TopicId}",
                Context.ConnectionId,
                topicId);
            return;
        }

        var groupName = $"topic:{topicId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "TrendHub client subscribed. ConnectionId={ConnectionId}. TopicId={TopicId}. Group={Group}",
            Context.ConnectionId,
            topicId,
            groupName);
    }
}
