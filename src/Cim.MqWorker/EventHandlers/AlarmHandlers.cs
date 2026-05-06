using Cim.DbAdapter.Models;
using Cim.DbAdapter.Repositories;
using Microsoft.Extensions.Logging;

namespace Cim.MqWorker.EventHandlers;

/// <summary>Handles AlarmRaisedEvent – inserts a new alarm record.</summary>
public class AlarmRaisedHandler
{
    private readonly IAlarmRepository _repo;
    private readonly ILogger<AlarmRaisedHandler> _logger;

    public AlarmRaisedHandler(IAlarmRepository repo, ILogger<AlarmRaisedHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task HandleAsync(AlarmRaisedEvent evt, CancellationToken ct)
    {
        _logger.LogWarning("Alarm [{Level}] {Code} on {Equipment}: {Desc}",
            evt.AlarmLevel, evt.AlarmCode, evt.EquipmentId, evt.Description);

        var alarm = new Alarm
        {
            EquipmentId = evt.EquipmentId,
            AlarmCode = evt.AlarmCode,
            AlarmLevel = evt.AlarmLevel,
            Description = evt.Description
        };

        await _repo.InsertAsync(alarm, ct);
    }
}

/// <summary>Handles AlarmClearedEvent – marks the alarm as cleared.</summary>
public class AlarmClearedHandler
{
    private readonly IAlarmRepository _repo;
    private readonly ILogger<AlarmClearedHandler> _logger;

    public AlarmClearedHandler(IAlarmRepository repo, ILogger<AlarmClearedHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task HandleAsync(AlarmClearedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation("Alarm {Code} cleared on {Equipment}", evt.AlarmCode, evt.EquipmentId);
        await _repo.ClearAlarmAsync(evt.EquipmentId, evt.AlarmCode, ct);
    }
}
