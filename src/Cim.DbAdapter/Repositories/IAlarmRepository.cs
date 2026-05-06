using Cim.DbAdapter.Models;

namespace Cim.DbAdapter.Repositories;

public interface IAlarmRepository
{
    Task<long> InsertAsync(Alarm alarm, CancellationToken ct = default);
    Task ClearAlarmAsync(string equipmentId, string alarmCode, CancellationToken ct = default);
    Task<IEnumerable<Alarm>> GetActiveByEquipmentAsync(string equipmentId, CancellationToken ct = default);
    Task<IEnumerable<Alarm>> GetAllActiveAsync(CancellationToken ct = default);
}
