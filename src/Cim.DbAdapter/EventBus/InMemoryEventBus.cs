using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cim.DbAdapter.EventBus;

/// <summary>
/// In-memory event bus built on System.Threading.Channels.
/// Drop-in replacement: swap this class with a RabbitMQ / Kafka implementation.
/// </summary>
public sealed class InMemoryEventBus : IEventBus, IAsyncDisposable
{
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();
    private readonly CancellationTokenSource _cts = new();

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    private Channel<T> GetOrCreateChannel<T>() where T : class
    {
        return (Channel<T>)_channels.GetOrAdd(typeof(T), type =>
        {
            var ch = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
            // Start a background pump for this channel
            var _ = PumpAsync(ch, _cts.Token);
            return ch;
        });
    }

    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var channel = GetOrCreateChannel<T>();
        await channel.Writer.WriteAsync(message, ct);
        _logger.LogDebug("Published {EventType} [{MsgId}]", typeof(T).Name,
            (message as Cim.DbAdapter.Models.CimEventMessage)?.MessageId ?? "-");
    }

    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class
    {
        GetOrCreateChannel<T>(); // ensure channel + pump exist
        var list = (List<Func<object, CancellationToken, Task>>)_handlers.GetOrAdd(typeof(T), _ => new List<Func<object, CancellationToken, Task>>());
        Func<object, CancellationToken, Task> wrapper = (obj, ct) => handler((T)obj, ct);
        lock (list) { list.Add(wrapper); }
        return new Subscription(() => { lock (list) { list.Remove(wrapper); } });
    }

    private async Task PumpAsync<T>(Channel<T> channel, CancellationToken ct) where T : class
    {
        await foreach (var msg in channel.Reader.ReadAllAsync(ct))
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) continue;
            List<Func<object, CancellationToken, Task>> snapshot;
            lock (list) { snapshot = list.ToList(); }
            foreach (var h in snapshot)
            {
                try { await h(msg, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Handler error for {EventType}", typeof(T).Name); }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        foreach (var ch in _channels.Values)
        {
            if (ch is IAsyncDisposable d) await d.DisposeAsync();
        }
        _cts.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
