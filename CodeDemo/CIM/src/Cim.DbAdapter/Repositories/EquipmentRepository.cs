using Dapper;
using Cim.DbAdapter.Models;
using Microsoft.Data.Sqlite;

namespace Cim.DbAdapter.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly string _connectionString;

    public EquipmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<EquipmentStatus?> GetByIdAsync(string equipmentId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<EquipmentStatus>(
            "SELECT * FROM EquipmentStatus WHERE EquipmentId = @EquipmentId",
            new { EquipmentId = equipmentId });
    }

    public async Task UpsertAsync(EquipmentStatus status, CancellationToken ct = default)
    {
        status.UpdatedAt = DateTime.UtcNow;
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO EquipmentStatus (EquipmentId, EquipmentName, State, RecipeId, LotId, UpdatedAt)
              VALUES (@EquipmentId, @EquipmentName, @State, @RecipeId, @LotId, @UpdatedAt)
              ON CONFLICT(EquipmentId) DO UPDATE SET
                EquipmentName = excluded.EquipmentName,
                State         = excluded.State,
                RecipeId      = excluded.RecipeId,
                LotId         = excluded.LotId,
                UpdatedAt     = excluded.UpdatedAt",
            status);
    }

    public async Task<IEnumerable<EquipmentStatus>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        return await conn.QueryAsync<EquipmentStatus>("SELECT * FROM EquipmentStatus ORDER BY EquipmentId");
    }
}
