using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Cim.DbAdapter.Schema;

/// <summary>
/// Initializes or migrates the SQLite schema on startup.
/// </summary>
public static class DbInitializer
{
    private static readonly string[] SchemaSql = new[]
    {
        @"CREATE TABLE IF NOT EXISTS EquipmentStatus (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            EquipmentId TEXT    NOT NULL UNIQUE,
            EquipmentName TEXT  NOT NULL DEFAULT '',
            State       TEXT    NOT NULL DEFAULT 'IDLE',
            RecipeId    TEXT,
            LotId       TEXT,
            UpdatedAt   TEXT    NOT NULL
        );",

        @"CREATE TABLE IF NOT EXISTS TrackEvents (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            SerialNumber TEXT    NOT NULL,
            LotId        TEXT    NOT NULL DEFAULT '',
            EquipmentId  TEXT    NOT NULL,
            StationId    TEXT    NOT NULL,
            EventType    TEXT    NOT NULL,
            RecipeId     TEXT,
            Operator     TEXT,
            EventTime    TEXT    NOT NULL,
            Remarks      TEXT
        );",

        @"CREATE INDEX IF NOT EXISTS IX_TrackEvents_SN     ON TrackEvents(SerialNumber);",
        @"CREATE INDEX IF NOT EXISTS IX_TrackEvents_LotId  ON TrackEvents(LotId);",

        @"CREATE TABLE IF NOT EXISTS TestResults (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            SerialNumber TEXT    NOT NULL,
            LotId        TEXT    NOT NULL DEFAULT '',
            EquipmentId  TEXT    NOT NULL,
            StationId    TEXT    NOT NULL,
            TestProgram  TEXT    NOT NULL DEFAULT '',
            Verdict      TEXT    NOT NULL,
            TestedAt     TEXT    NOT NULL,
            Operator     TEXT,
            UNIQUE(SerialNumber, StationId)
        );",

        @"CREATE INDEX IF NOT EXISTS IX_TestResults_SN    ON TestResults(SerialNumber);",
        @"CREATE INDEX IF NOT EXISTS IX_TestResults_LotId ON TestResults(LotId);",

        @"CREATE TABLE IF NOT EXISTS TestItems (
            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
            TestResultId  INTEGER NOT NULL REFERENCES TestResults(Id) ON DELETE CASCADE,
            ItemName      TEXT    NOT NULL,
            MeasuredValue REAL,
            LowerLimit    REAL,
            UpperLimit    REAL,
            Unit          TEXT,
            Verdict       TEXT    NOT NULL
        );",

        @"CREATE TABLE IF NOT EXISTS Alarms (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            EquipmentId TEXT    NOT NULL,
            AlarmCode   TEXT    NOT NULL,
            AlarmLevel  TEXT    NOT NULL DEFAULT 'WARNING',
            Description TEXT    NOT NULL DEFAULT '',
            Status      TEXT    NOT NULL DEFAULT 'ACTIVE',
            RaisedAt    TEXT    NOT NULL,
            ClearedAt   TEXT
        );",

        @"CREATE INDEX IF NOT EXISTS IX_Alarms_Equipment ON Alarms(EquipmentId, Status);"
    };

    public static async Task InitializeAsync(string connectionString, ILogger? logger = null)
    {
        // Ensure the data directory exists
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dbPath = builder.DataSource;
        if (!string.IsNullOrWhiteSpace(dbPath) && dbPath != ":memory:")
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        // Enable WAL mode for better concurrent access
        await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await conn.ExecuteAsync("PRAGMA foreign_keys=ON;");

        foreach (var sql in SchemaSql)
        {
            await conn.ExecuteAsync(sql);
        }

        logger?.LogInformation("Database schema initialized at {DbPath}", dbPath);
    }
}
