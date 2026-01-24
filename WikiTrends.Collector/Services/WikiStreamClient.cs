using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WikiTrends.Collector.Models;

namespace WikiTrends.Collector.Services;

/// <summary>
/// Реализация клиента для Wikipedia EventStreams SSE.
/// </summary>
public sealed class WikiStreamClient : IWikiStreamClient
{
    private readonly HttpClient _httpClient;
    private readonly WikiStreamOptions _options;
    private readonly ILogger<WikiStreamClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private int _incompleteEventsLogged;
    private int _sampleDataLogged;
    private int _fieldDiagnosticsLogged;

    private HttpResponseMessage? _response;
    private StreamReader? _reader;
    private bool _disposed;

    public WikiStreamClient(
        HttpClient httpClient,
        IOptions<WikiStreamOptions> options,
        ILogger<WikiStreamClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    /// <inheritdoc />
    public bool IsConnected => _reader is not null && _response is not null;

    /// <inheritdoc />
    public string? LastEventId { get; private set; }

    /// <inheritdoc />
    public async Task ConnectAsync(string? lastEventId = null, CancellationToken ct = default)
    {
        if (IsConnected)
        {
            _logger.LogDebug("Closing existing connection before reconnect");
            Disconnect();
        }

        var url = _options.StreamUrl;
        _logger.LogDebug("Connecting to SSE stream: {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (!string.IsNullOrEmpty(lastEventId))
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
            _logger.LogDebug("Resuming from Last-Event-ID: {LastEventId}", lastEventId);
        }

        _response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!_response.IsSuccessStatusCode)
        {
            var statusCode = _response.StatusCode;
            _response.Dispose();
            _response = null;

            _logger.LogError("Failed to connect to SSE stream. Status: {StatusCode}", statusCode);
            throw new HttpRequestException(
                $"Failed to connect to SSE stream. Status code: {statusCode}");
        }

        var stream = await _response.Content.ReadAsStreamAsync(ct);

        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);

        _logger.LogInformation("Successfully connected to SSE stream: {Url}", url);
    }

    /// <inheritdoc />
    public async Task<WikiRecentChange?> ReadEventAsync(CancellationToken ct = default)
    {
        if (_reader is null)
        {
            throw new InvalidOperationException(
                "Not connected. Call ConnectAsync first.");
        }
        var dataBuilder = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            var line = await _reader.ReadLineAsync(ct);
            if (line is null)
            {
                _logger.LogWarning("SSE stream ended (null line received)");
                return null;  // Сигнал о закрытии соединения
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataBuilder.Length == 0)
                {
                    continue;
                }

                var data = dataBuilder.ToString().Trim();
                dataBuilder.Clear();

                if (_sampleDataLogged < 3)
                {
                    _sampleDataLogged++;
                    _logger.LogDebug(
                        "SSE sample payload: {Data}",
                        data.Length > 300 ? data[..300] + "..." : data);
                }

                try
                {
                    var change = JsonSerializer.Deserialize<WikiRecentChange>(data, _jsonOptions);
                    if (change is not null)
                    {
                        if (_incompleteEventsLogged < 5 &&
                            (change.PageId is null || string.IsNullOrWhiteSpace(change.Title) || string.IsNullOrWhiteSpace(change.User) ||
                             string.IsNullOrWhiteSpace(change.Wiki) || string.IsNullOrWhiteSpace(change.Type)))
                        {
                            _incompleteEventsLogged++;
                            _logger.LogDebug(
                                "SSE event deserialized but missing fields (page_id/title/user/wiki/type). Raw data: {Data}",
                                data.Length > 300 ? data[..300] + "..." : data);

                            if (_fieldDiagnosticsLogged < 3)
                            {
                                _fieldDiagnosticsLogged++;
                                try
                                {
                                    using var doc = JsonDocument.Parse(data);
                                    var root = doc.RootElement;

                                    var hasPageId = root.TryGetProperty("page_id", out var pageIdEl);
                                    var hasTitle = root.TryGetProperty("title", out var titleEl);
                                    var hasUser = root.TryGetProperty("user", out var userEl);
                                    var hasWiki = root.TryGetProperty("wiki", out var wikiEl);
                                    var hasType = root.TryGetProperty("type", out var typeEl);
                                    var hasNamespace = root.TryGetProperty("namespace", out var nsEl);

                                    _logger.LogDebug(
                                        "SSE field diagnostics: page_id={HasPageId}({PageIdKind}), title={HasTitle}({TitleKind}), user={HasUser}({UserKind}), wiki={HasWiki}({WikiKind}), type={HasType}({TypeKind}), namespace={HasNamespace}({NamespaceKind})",
                                        hasPageId, hasPageId ? pageIdEl.ValueKind : (JsonValueKind?)null,
                                        hasTitle, hasTitle ? titleEl.ValueKind : (JsonValueKind?)null,
                                        hasUser, hasUser ? userEl.ValueKind : (JsonValueKind?)null,
                                        hasWiki, hasWiki ? wikiEl.ValueKind : (JsonValueKind?)null,
                                        hasType, hasType ? typeEl.ValueKind : (JsonValueKind?)null,
                                        hasNamespace, hasNamespace ? nsEl.ValueKind : (JsonValueKind?)null);

                                    _logger.LogDebug(
                                        "SSE field diagnostics values: type={Type}, wiki={Wiki}, title={Title}",
                                        hasType ? typeEl.GetString() : null,
                                        hasWiki ? wikiEl.GetString() : null,
                                        hasTitle ? titleEl.GetString() : null);

                                    var rootKeys = string.Join(",", root.EnumerateObject().Select(p => p.Name));
                                    _logger.LogDebug("SSE root keys: {Keys}", rootKeys);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to parse SSE payload for diagnostics.");
                                }
                            }
                        }

                        _logger.LogTrace(
                            "Parsed event: {Wiki}/{Title} by {User}",
                            change.Wiki, change.Title, change.User);

                        return change;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize SSE event: {Data}",
                        data.Length > 200 ? data[..200] + "..." : data);
                }

                continue;
            }

            if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                LastEventId = line.Substring(3).Trim();
                _logger.LogTrace("Received event ID: {EventId}", LastEventId);
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var dataLine = line.Substring(5);
                dataBuilder.AppendLine(dataLine);
                continue;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
            {
                // Это комментарий SSE, используется для heartbeat
                _logger.LogTrace("Received SSE comment: {Line}", line);
            }
            
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        //  Закрыть и dispose StreamReader
        //  Закрыть и dispose HttpResponseMessage
        //  Установить _reader и _response в null
        //  Залогировать отключение
        if (_reader is not null)
        {
            try
            {
                _reader.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing StreamReader");
            }
            _reader = null;
        }

        if (_response is not null)
        {
            try
            {
                _response.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing HttpResponseMessage");
            }
            _response = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        //  Если уже disposed — выйти
        //  Установить _disposed = true
        //  Вызвать Disconnect()
        //  Подавить GC.SuppressFinalize
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}