using Dapper;
using Cim.DbAdapter.Models;
using Microsoft.Data.Sqlite;

namespace Cim.DbAdapter.Repositories;

public class AlarmRepository : IAlarmRepository
{
    private readonly string _connectionString;

    public AlarmRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> InsertAsync(Alarm alarm, CancellationToken ct = default)
    {
        alarm.RaisedAt = DateTime.UtcNow;
        alarm.Status = "ACTIVE";
        using var conn = new SqliteConnection(_connectionString);
        return await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO Alarms (EquipmentId, AlarmCode, AlarmLevel, Description, Status, RaisedAt, ClearedAt)
              VALUES (@EquipmentId, @AlarmCode, @AlarmLevel, @Description, @Status, @RaisedAt, @ClearedAt);
              SELECT last_insert_rowid();",
            alarm);
    }

    public async Task ClearAlarmAsync(string equipmentId, string alarmCode, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(
            @"UPDATE Alarms SET Status = 'CLEARED', ClearedAt = @Now
              WHERE EquipmentId = @EquipmentId AND AlarmCode = @AlarmCode AND Status = 'ACTIVE'",
            new { Now = DateTime.UtcNow, EquipmentId = equipmentId, AlarmCode = alarmCode });
    }

    public async Task<IEnumerable<Alarm>> GetActiveByEquipmentAsync(string equipmentId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryAsync<Alarm>(
            "SELECT * FROM Alarms WHERE EquipmentId = @EquipmentId AND Status = 'ACTIVE' ORDER BY RaisedAt DESC",
            new { EquipmentId = equipmentId });
    }

    public async Task<IEnumerable<Alarm>> GetAllActiveAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryAsync<Alarm>(
            "SELECT * FROM Alarms WHERE Status = 'ACTIVE' ORDER BY RaisedAt DESC");
    }
}
