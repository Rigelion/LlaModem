using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// Lightweight SQLite helper for usage record persistence.
/// Creates the table on first use if it doesn't exist.
/// </summary>
public sealed class UsageDbContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public UsageDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqliteConnection Connection
    {
        get
        {
            if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
            {
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
            }
            return _connection;
        }
    }

    public void EnsureCreated()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_records (
                id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp               TEXT    NOT NULL,
                request_id              TEXT,
                created                 TEXT,
                model                   TEXT    NOT NULL,
                route                   TEXT    NOT NULL,
                prompt_tokens           INTEGER NOT NULL,
                completion_tokens       INTEGER NOT NULL,
                total_tokens            INTEGER NOT NULL,
                prompt_ms               REAL,
                completion_ms           REAL,
                prompt_per_token_ms     REAL,
                completion_per_token_ms REAL,
                cache_hits              INTEGER,
                cached_tokens           INTEGER
            );
            CREATE INDEX IF NOT EXISTS idx_usage_records_timestamp ON usage_records(timestamp);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Insert(
        string timestamp,
        string? requestId,
        string? created,
        string model,
        string route,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        double? promptMs,
        double? completionMs,
        double? promptPerTokenMs,
        double? completionPerTokenMs,
        int? cacheHits,
        int? cachedTokens)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage_records
                (timestamp, request_id, created, model, route, prompt_tokens, completion_tokens, total_tokens,
                 prompt_ms, completion_ms, prompt_per_token_ms, completion_per_token_ms, cache_hits, cached_tokens)
            VALUES
                (@timestamp, @requestId, @created, @model, @route, @promptTokens, @completionTokens, @totalTokens,
                 @promptMs, @completionMs, @promptPerTokenMs, @completionPerTokenMs, @cacheHits, @cachedTokens);
            """;

        cmd.Parameters.AddWithValue("@timestamp", timestamp);
        cmd.Parameters.AddWithValue("@requestId", (object?)requestId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", (object?)created ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@model", model);
        cmd.Parameters.AddWithValue("@route", route);
        cmd.Parameters.AddWithValue("@promptTokens", promptTokens);
        cmd.Parameters.AddWithValue("@completionTokens", completionTokens);
        cmd.Parameters.AddWithValue("@totalTokens", totalTokens);
        cmd.Parameters.AddWithValue("@promptMs", (object?)promptMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@completionMs", (object?)completionMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@promptPerTokenMs", (object?)promptPerTokenMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@completionPerTokenMs", (object?)completionPerTokenMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cacheHits", (object?)cacheHits ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cachedTokens", (object?)cachedTokens ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
