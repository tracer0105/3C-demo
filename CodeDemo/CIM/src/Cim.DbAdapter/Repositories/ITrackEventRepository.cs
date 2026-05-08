using Cim.DbAdapter.Models;

namespace Cim.DbAdapter.Repositories;

public interface ITrackEventRepository
{
    Task<long> InsertAsync(TrackEvent trackEvent, CancellationToken ct = default);
    Task<IEnumerable<TrackEvent>> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<IEnumerable<TrackEvent>> GetByLotIdAsync(string lotId, CancellationToken ct = default);
}
