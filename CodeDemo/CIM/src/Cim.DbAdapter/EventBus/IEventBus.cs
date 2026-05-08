namespace Cim.DbAdapter.EventBus;

/// <summary>
/// Lightweight event bus abstraction – swappable with RabbitMQ / Kafka.
/// </summary>
public interface IEventBus
{
    /// <summary>Publish an event to all subscribers of type <typeparamref name="T"/>.</summary>
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;

    /// <summary>Subscribe to events of type <typeparamref name="T"/>.</summary>
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class;
}
