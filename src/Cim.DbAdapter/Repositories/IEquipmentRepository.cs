using Cim.DbAdapter.Models;

namespace Cim.DbAdapter.Repositories;

public interface IEquipmentRepository
{
    Task<EquipmentStatus?> GetByIdAsync(string equipmentId, CancellationToken ct = default);
    Task UpsertAsync(EquipmentStatus status, CancellationToken ct = default);
    Task<IEnumerable<EquipmentStatus>> GetAllAsync(CancellationToken ct = default);
}
