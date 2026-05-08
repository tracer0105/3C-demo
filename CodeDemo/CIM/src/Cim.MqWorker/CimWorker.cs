using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Models;
using Cim.MqWorker.EventHandlers;

namespace Cim.MqWorker;

/// <summary>
/// Background worker that subscribes to the in-memory event bus and dispatches
/// events to the appropriate handlers which write to the database.
/// Replace InMemoryEventBus with a RabbitMQ/Kafka adapter to go production-ready.
/// </summary>
public class CimWorker : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly EquipmentStateChangedHandler _equipmentHandler;
    private readonly AlarmRaisedHandler _alarmRaisedHandler;
    private readonly AlarmClearedHandler _alarmClearedHandler;
    private readonly TestResultPublishedHandler _testResultHandler;
    private readonly ILogger<CimWorker> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    public CimWorker(
        IEventBus eventBus,
        EquipmentStateChangedHandler equipmentHandler,
        AlarmRaisedHandler alarmRaisedHandler,
        AlarmClearedHandler alarmClearedHandler,
        TestResultPublishedHandler testResultHandler,
        ILogger<CimWorker> logger)
    {
        _eventBus = eventBus;
        _equipmentHandler = equipmentHandler;
        _alarmRaisedHandler = alarmRaisedHandler;
        _alarmClearedHandler = alarmClearedHandler;
        _testResultHandler = testResultHandler;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CimWorker starting – subscribing to event bus.");

        _subscriptions.Add(_eventBus.Subscribe<EquipmentStateChangedEvent>(
            (evt, ct) => _equipmentHandler.HandleAsync(evt, ct)));

        _subscriptions.Add(_eventBus.Subscribe<AlarmRaisedEvent>(
            (evt, ct) => _alarmRaisedHandler.HandleAsync(evt, ct)));

        _subscriptions.Add(_eventBus.Subscribe<AlarmClearedEvent>(
            (evt, ct) => _alarmClearedHandler.HandleAsync(evt, ct)));

        _subscriptions.Add(_eventBus.Subscribe<TestResultPublishedEvent>(
            (evt, ct) => _testResultHandler.HandleAsync(evt, ct)));

        _logger.LogInformation("CimWorker listening. Waiting for events…");

        // Keep running until cancellation
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CimWorker stopping – releasing subscriptions.");
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        await base.StopAsync(cancellationToken);
    }
}
