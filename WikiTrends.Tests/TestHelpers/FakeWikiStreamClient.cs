using WikiTrends.Collector.Models;
using WikiTrends.Collector.Services;

namespace WikiTrends.Tests.TestHelpers;

internal sealed class FakeWikiStreamClient : IWikiStreamClient
{
    private readonly Queue<WikiRecentChange?> _events;

    public FakeWikiStreamClient(IEnumerable<WikiRecentChange?> events)
    {
        _events = new Queue<WikiRecentChange?>(events);
    }

    public bool IsConnected { get; private set; }

    public string? LastEventId { get; private set; }

    public Task ConnectAsync(string? lastEventId = null, CancellationToken ct = default)
    {
        IsConnected = true;
        LastEventId = lastEventId;
        return Task.CompletedTask;
    }

    public Task<WikiRecentChange?> ReadEventAsync(CancellationToken ct = default)
    {
        if (_events.Count == 0)
        {
            IsConnected = false;
            return Task.FromResult<WikiRecentChange?>(null);
        }

        var next = _events.Dequeue();
        if (next == null)
        {
            IsConnected = false;
        }

        return Task.FromResult(next);
    }

    public void Disconnect()
    {
        IsConnected = false;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
