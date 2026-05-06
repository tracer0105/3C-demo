using Dapper;
using Cim.DbAdapter.Models;
using Microsoft.Data.Sqlite;

namespace Cim.DbAdapter.Repositories;

public class TrackEventRepository : ITrackEventRepository
{
    private readonly string _connectionString;

    public TrackEventRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> InsertAsync(TrackEvent trackEvent, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        trackEvent.EventTime = DateTime.UtcNow;
        return await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO TrackEvents (SerialNumber, LotId, EquipmentId, StationId, EventType, RecipeId, Operator, EventTime, Remarks)
              VALUES (@SerialNumber, @LotId, @EquipmentId, @StationId, @EventType, @RecipeId, @Operator, @EventTime, @Remarks);
              SELECT last_insert_rowid();",
            trackEvent);
    }

    public async Task<IEnumerable<TrackEvent>> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryAsync<TrackEvent>(
            "SELECT * FROM TrackEvents WHERE SerialNumber = @SerialNumber ORDER BY EventTime DESC",
            new { SerialNumber = serialNumber });
    }

    public async Task<IEnumerable<TrackEvent>> GetByLotIdAsync(string lotId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryAsync<TrackEvent>(
            "SELECT * FROM TrackEvents WHERE LotId = @LotId ORDER BY EventTime DESC",
            new { LotId = lotId });
    }
}
