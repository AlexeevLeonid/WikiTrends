using WikiTrends.Analytics.Services;
using WikiTrends.Contracts.Events;
using WikiTrends.Infrastructure.Kafka.Consumer;

namespace WikiTrends.Analytics.Handlers;

public sealed class ClassifiedEditHandler : IMessageHandler<ClassifiedEditEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClassifiedEditHandler> _logger;

    public ClassifiedEditHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ClassifiedEditHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(ClassifiedEditEvent message, CancellationToken cancellationToken)
    {
        if (message == null)
        {
            _logger.LogWarning("Received null ClassifiedEditEvent.");
            return;
        }

        using var scope = _logger.BeginScope(new
        {
            message.Wiki,
            message.Title,
            message.ArticleId,
            message.WikiEditId,
            message.EventId
        });

        _logger.LogDebug(
            "Handling ClassifiedEditEvent. Wiki={Wiki} Title={Title} ArticleId={ArticleId} WikiEditId={WikiEditId}",
            message.Wiki,
            message.Title,
            message.ArticleId,
            message.WikiEditId);

        try
        {
            await using var diScope = _scopeFactory.CreateAsyncScope();
            var ingestionService = diScope.ServiceProvider.GetRequiredService<IEventIngestionService>();
            await ingestionService.IngestAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle ClassifiedEditEvent.");
        }
    }
}
