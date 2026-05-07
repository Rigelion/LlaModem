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

        // Create table if it doesn't exist
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
                cached_tokens           INTEGER,
                request_time            TEXT,
                response_time           TEXT,
                client_ip               TEXT,
                status_code             INTEGER NOT NULL DEFAULT 200,
                request_headers         TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_usage_records_timestamp ON usage_records(timestamp);
            """;
        cmd.ExecuteNonQuery();

        // Migrate existing databases that lack the new columns
        cmd.CommandText = "PRAGMA table_info(usage_records)";
        using var reader = cmd.ExecuteReader();
        var columns = new HashSet<string>();
        while (reader.Read())
            columns.Add(reader.GetString(reader.GetOrdinal("name")));

        var alterParts = new List<string>();
        if (!columns.Contains("request_time"))
            alterParts.Add("ADD COLUMN request_time TEXT");
        if (!columns.Contains("response_time"))
            alterParts.Add("ADD COLUMN response_time TEXT");
        if (!columns.Contains("client_ip"))
            alterParts.Add("ADD COLUMN client_ip TEXT");
        if (!columns.Contains("status_code"))
            alterParts.Add("ADD COLUMN status_code INTEGER DEFAULT 200");
        if (!columns.Contains("request_headers"))
            alterParts.Add("ADD COLUMN request_headers TEXT");

        if (alterParts.Count > 0)
        {
            var alterSql = $"ALTER TABLE usage_records {string.Join(", ", alterParts)}";
            cmd.CommandText = alterSql;
            cmd.ExecuteNonQuery();
        }
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
        int? cachedTokens,
        string? requestTime = null,
        string? responseTime = null,
        string? clientIp = null,
        int statusCode = 200,
        string? requestHeaders = null)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage_records
                (timestamp, request_id, created, model, route, prompt_tokens, completion_tokens, total_tokens,
                 prompt_ms, completion_ms, prompt_per_token_ms, completion_per_token_ms, cache_hits, cached_tokens,
                 request_time, response_time, client_ip, status_code, request_headers)
            VALUES
                (@timestamp, @requestId, @created, @model, @route, @promptTokens, @completionTokens, @totalTokens,
                 @promptMs, @completionMs, @promptPerTokenMs, @completionPerTokenMs, @cacheHits, @cachedTokens,
                 @requestTime, @responseTime, @clientIp, @statusCode, @requestHeaders);
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
        cmd.Parameters.AddWithValue("@requestTime", (object?)requestTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@responseTime", (object?)responseTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@clientIp", (object?)clientIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@statusCode", statusCode);
        cmd.Parameters.AddWithValue("@requestHeaders", (object?)requestHeaders ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
