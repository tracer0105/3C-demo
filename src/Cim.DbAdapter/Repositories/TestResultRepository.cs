using Dapper;
using Cim.DbAdapter.Models;
using Microsoft.Data.Sqlite;

namespace Cim.DbAdapter.Repositories;

public class TestResultRepository : ITestResultRepository
{
    private readonly string _connectionString;

    public TestResultRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> UpsertAsync(TestResult result, CancellationToken ct = default)
    {
        result.TestedAt = DateTime.UtcNow;
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        // Upsert header
        var resultId = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO TestResults (SerialNumber, LotId, EquipmentId, StationId, TestProgram, Verdict, TestedAt, Operator)
              VALUES (@SerialNumber, @LotId, @EquipmentId, @StationId, @TestProgram, @Verdict, @TestedAt, @Operator)
              ON CONFLICT(SerialNumber, StationId) DO UPDATE SET
                LotId       = excluded.LotId,
                EquipmentId = excluded.EquipmentId,
                TestProgram = excluded.TestProgram,
                Verdict     = excluded.Verdict,
                TestedAt    = excluded.TestedAt,
                Operator    = excluded.Operator;
              SELECT Id FROM TestResults WHERE SerialNumber = @SerialNumber AND StationId = @StationId;",
            result, tx);

        // Remove old items and re-insert
        await conn.ExecuteAsync("DELETE FROM TestItems WHERE TestResultId = @Id", new { Id = resultId }, tx);
        foreach (var item in result.Items)
        {
            item.TestResultId = resultId;
            await conn.ExecuteAsync(
                @"INSERT INTO TestItems (TestResultId, ItemName, MeasuredValue, LowerLimit, UpperLimit, Unit, Verdict)
                  VALUES (@TestResultId, @ItemName, @MeasuredValue, @LowerLimit, @UpperLimit, @Unit, @Verdict)",
                item, tx);
        }

        tx.Commit();
        return resultId;
    }

    public async Task<TestResult?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var result = await conn.QueryFirstOrDefaultAsync<TestResult>(
            "SELECT * FROM TestResults WHERE SerialNumber = @SerialNumber ORDER BY TestedAt DESC LIMIT 1",
            new { SerialNumber = serialNumber });

        if (result is null) return null;

        result.Items = (await conn.QueryAsync<TestItem>(
            "SELECT * FROM TestItems WHERE TestResultId = @Id",
            new { result.Id })).AsList();

        return result;
    }

    public async Task<IEnumerable<TestResult>> GetByLotIdAsync(string lotId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var results = (await conn.QueryAsync<TestResult>(
            "SELECT * FROM TestResults WHERE LotId = @LotId ORDER BY TestedAt DESC",
            new { LotId = lotId })).AsList();

        foreach (var r in results)
        {
            r.Items = (await conn.QueryAsync<TestItem>(
                "SELECT * FROM TestItems WHERE TestResultId = @Id",
                new { r.Id })).AsList();
        }

        return results;
    }
}
