using Cim.DbAdapter.EventBus;
using Cim.DbAdapter.Models;
using Cim.DbAdapter.Repositories;
using Microsoft.Extensions.Logging;

namespace Cim.MqWorker.EventHandlers;

/// <summary>Handles EquipmentStateChangedEvent – persists the new equipment status.</summary>
public class EquipmentStateChangedHandler
{
    private readonly IEquipmentRepository _repo;
    private readonly ILogger<EquipmentStateChangedHandler> _logger;

    public EquipmentStateChangedHandler(IEquipmentRepository repo, ILogger<EquipmentStateChangedHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task HandleAsync(EquipmentStateChangedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation("Equipment {Id} state: {Prev} → {New}", evt.EquipmentId, evt.PreviousState, evt.NewState);

        var status = await _repo.GetByIdAsync(evt.EquipmentId, ct) ?? new EquipmentStatus
        {
            EquipmentId = evt.EquipmentId,
            EquipmentName = evt.EquipmentName
        };

        status.EquipmentName = evt.EquipmentName;
        status.State = evt.NewState;
        status.RecipeId = evt.RecipeId;
        status.LotId = evt.LotId;

        await _repo.UpsertAsync(status, ct);
    }
}
