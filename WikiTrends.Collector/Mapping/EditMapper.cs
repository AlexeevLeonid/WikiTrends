using Microsoft.Extensions.Options;
using WikiTrends.Collector.Models;
using WikiTrends.Collector.Services;
using WikiTrends.Contracts.Events;

namespace WikiTrends.Collector.Mapping;

/// <summary>
/// Реализация маппера для преобразования Wikipedia событий в доменные события.
/// </summary>
public sealed class EditMapper : IEditMapper
{
    private readonly WikiStreamOptions _options;
    private readonly ILogger<EditMapper> _logger;
    private readonly HashSet<string> _allowedWikis;
    private readonly HashSet<int> _allowedNamespaces;
    private readonly HashSet<string> _allowedTypes;

    private int _missingFieldsLogged;
    private int _notAllowedWikiLogged;
    private int _notAllowedTypeLogged;
    private int _notAllowedNamespaceLogged;

    public EditMapper(
        IOptions<WikiStreamOptions> options,
        ILogger<EditMapper> logger)
    {
        _options = options.Value;
        _logger = logger;
        _allowedWikis = _options.AllowedWikis.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allowedNamespaces = _options.AllowedNamespaces.ToHashSet();
        _allowedTypes = _options.AllowedTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public RawEditEvent? Map(WikiRecentChange change)
    {
        if (change != null)
        {
            if (change.Id == null || change.Title == null || change.User == null) 
            {
                if (_missingFieldsLogged < 20)
                {
                    _missingFieldsLogged++;
                    _logger.LogDebug("One of required fields has been null {0}", change.Id);
                }
                return null;
            };

            if (string.IsNullOrWhiteSpace(change.Wiki) || !_allowedWikis.Contains(change.Wiki))
            {
                if (_notAllowedWikiLogged < 50)
                {
                    _notAllowedWikiLogged++;
                    _logger.LogDebug("Wiki is null or not aloowed id: {0}, wiki: {1}",
                        change.Id, change.Wiki == null ? "null" : change.Wiki);
                }
                return null;
            }
            if (change.Type != null && !_allowedTypes.Contains(change.Type))
            {
                if (_notAllowedTypeLogged < 50)
                {
                    _notAllowedTypeLogged++;
                    _logger.LogDebug("Type is null or not aloowed id: {0}, type: {1}",
                        change.Id, change.Type == null ? "null" : change.Type);
                }
                return null;
            }
            if (!_allowedNamespaces.Contains(change.Namespace))
            {
                if (_notAllowedNamespaceLogged < 50)
                {
                    _notAllowedNamespaceLogged++;
                    _logger.LogDebug("Namespace is null or not aloowed id: {0}, namespace {1}",
                        change.Id, change.Namespace == null ? "null" : change.Namespace);
                }
                return null;
            }
            return new RawEditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = change.Timestamp > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(change.Timestamp)
                    : DateTimeOffset.FromUnixTimeSeconds(change.Timestamp),
                WikiEditId = change.Id.Value,
                PageId = change.PageId ?? 0,
                Title = change.Title,
                Wiki = change.Wiki,
                User = change.User,
                IsBot = change.Bot,
                IsMinor = change.Minor,
                IsNew = change.Type == "new",
                OldLength = change.Length?.Old.Value ?? 0,
                NewLength = change.Length?.New.Value ?? 0,

                CollectedAt = DateTimeOffset.UtcNow
            };
        }
        else return null;
    }
}