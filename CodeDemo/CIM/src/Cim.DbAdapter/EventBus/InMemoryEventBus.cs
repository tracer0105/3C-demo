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
    // Keyed by event type; values are ChannelHolder<T> instances (type-safe wrappers).
    private readonly ConcurrentDictionary<Type, IChannelHolder> _channels = new();
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();
    private readonly CancellationTokenSource _cts = new();

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    private ChannelHolder<T> GetOrCreateChannel<T>() where T : class
    {
        return (ChannelHolder<T>)_channels.GetOrAdd(typeof(T), type =>
        {
            var holder = new ChannelHolder<T>();
            var pumpTask = PumpAsync(holder.Channel, _cts.Token);
            pumpTask.ContinueWith(
                t => _logger.LogError(t.Exception, "Event pump error for {EventType}", typeof(T).Name),
                TaskContinuationOptions.OnlyOnFaulted);
            return holder;
        });
    }

    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var holder = GetOrCreateChannel<T>();
        await holder.Channel.Writer.WriteAsync(message, ct);
        _logger.LogDebug("Published {EventType} [{MsgId}]", typeof(T).Name,
            (message as Cim.DbAdapter.Models.CimEventMessage)?.MessageId ?? "-");
    }

    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class
    {
        GetOrCreateChannel<T>(); // ensure channel + pump exist
        var list = (List<Func<object, CancellationToken, Task>>)_handlers.GetOrAdd(
            typeof(T), _ => new List<Func<object, CancellationToken, Task>>());
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
        foreach (var holder in _channels.Values)
            await holder.DisposeAsync();
        _cts.Dispose();
    }

    // ─── Type-safe channel wrapper ─────────────────────────────────────────

    private interface IChannelHolder : IAsyncDisposable { }

    private sealed class ChannelHolder<T> : IChannelHolder where T : class
    {
        public Channel<T> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        public ValueTask DisposeAsync()
        {
            Channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
