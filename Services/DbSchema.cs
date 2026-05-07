using Dapper;
using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// Shared SQLite schema definitions for the usage records table.
/// Used by both write (UsageService) and read (StatsService) paths.
/// </summary>
public static class DbSchema
{
    /// <summary>
    /// Full CREATE TABLE statement for usage_records.
    /// </summary>
    public const string TableSql = """
        CREATE TABLE IF NOT EXISTS usage_records (
            id                      INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp               TEXT    NOT NULL,
            request_id              TEXT,
            created                 TEXT,
            model                   TEXT    NOT NULL,
            route                   TEXT    NOT NULL,
            prompt_tokens           INTEGER NOT NULL DEFAULT 0,
            completion_tokens       INTEGER NOT NULL DEFAULT 0,
            total_tokens            INTEGER NOT NULL DEFAULT 0,
            prompt_ms               REAL,
            completion_ms           REAL,
            prompt_per_token_ms     REAL,
            completion_per_token_ms REAL,
            cache_hits              INTEGER,
            cached_tokens           INTEGER,
            request_time            TEXT,
            response_time           TEXT,
            client_ip               TEXT,
            status_code             INTEGER NOT NULL DEFAULT 200,
            request_headers         TEXT
        )
        """;

    /// <summary>
    /// Index on timestamp for date-range queries.
    /// </summary>
    public const string IndexTimestampSql =
        "CREATE INDEX IF NOT EXISTS ix_usage_timestamp ON usage_records(timestamp)";

    /// <summary>
    /// Index on model for filtered queries.
    /// </summary>
    public const string IndexModelSql =
        "CREATE INDEX IF NOT EXISTS ix_usage_model ON usage_records(model)";

    /// <summary>
    /// Ensures the schema exists and is up to date.
    /// Creates the table and indexes, then adds any missing columns via migration.
    /// </summary>
    public static async Task EnsureAsync(SqliteConnection conn)
    {
        await conn.ExecuteAsync(TableSql);
        await conn.ExecuteAsync(IndexTimestampSql);
        await conn.ExecuteAsync(IndexModelSql);

        // Migrate: add columns that may be missing from older databases.
        // Silently ignores "duplicate column" errors (column already exists).
        var migrationSqls = new[]
        {
            "ALTER TABLE usage_records ADD COLUMN request_id TEXT",
            "ALTER TABLE usage_records ADD COLUMN created TEXT",
            "ALTER TABLE usage_records ADD COLUMN prompt_per_token_ms REAL",
            "ALTER TABLE usage_records ADD COLUMN completion_per_token_ms REAL",
            "ALTER TABLE usage_records ADD COLUMN cache_hits INTEGER",
            "ALTER TABLE usage_records ADD COLUMN cached_tokens INTEGER",
            "ALTER TABLE usage_records ADD COLUMN request_time TEXT",
            "ALTER TABLE usage_records ADD COLUMN response_time TEXT",
            "ALTER TABLE usage_records ADD COLUMN client_ip TEXT",
            "ALTER TABLE usage_records ADD COLUMN status_code INTEGER DEFAULT 200",
            "ALTER TABLE usage_records ADD COLUMN request_headers TEXT",
        };

        foreach (var sql in migrationSqls)
        {
            try
            {
                await conn.ExecuteAsync(sql);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR: duplicate column
            {
                // Column already exists — harmless.
            }
        }
    }
}
