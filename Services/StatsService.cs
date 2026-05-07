using LlaModem.Models;
using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// SQLite-backed implementation for usage statistics queries.
/// Reuses UsageDbContext for connection management.
/// </summary>
public sealed class StatsService : IStatsService, IDisposable
{
    private readonly string _connectionString;

    public StatsService(string connectionString)
    {
        _connectionString = connectionString;
        // Ensure the database directory exists (same as UsageService)
        var dbPath = connectionString.Replace("Data Source=", "");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");

        await EnsureTableCreatedAsync();

        // Daily aggregation
        var dailySql = $"""
            SELECT date(timestamp)            as day,
                   model,
                   COUNT(*)                     as requests,
                   SUM(prompt_tokens)           as prompt_tokens,
                   SUM(completion_tokens)       as completion_tokens,
                   SUM(total_tokens)            as total_tokens,
                   AVG(prompt_ms)               as avg_prompt_ms,
                   AVG(completion_ms)           as avg_completion_ms,
                   CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) as cache_hit_rate
            FROM usage_records
            WHERE timestamp >= @cutoff
              {GetModelClause(model)}
            GROUP BY day, model
            ORDER BY day
            """;

        // Summary aggregation
        var summarySql = $"""
            SELECT COUNT(*)                                          as total_requests,
                   COALESCE(SUM(prompt_tokens), 0)                    as total_prompt_tokens,
                   COALESCE(SUM(completion_tokens), 0)                as total_completion_tokens,
                   COALESCE(SUM(total_tokens), 0)                     as total_tokens,
                   COALESCE(AVG(prompt_ms), 0)                        as avg_prompt_ms,
                   COALESCE(AVG(completion_ms), 0)                    as avg_completion_ms,
                   CASE WHEN COUNT(*) > 0 THEN CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) ELSE 0 END as cache_hit_rate
            FROM usage_records
            WHERE timestamp >= @cutoff
              {GetModelClause(model)}
            """;

        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        DailyUsageRow[] daily;
        UsageSummaryRow summary;

        // Fetch daily rows
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = dailySql;
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            AddModelParam(cmd, model);

            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<DailyUsageRow>();
            while (await reader.ReadAsync())
            {
                rows.Add(new DailyUsageRow(
                    Date: reader.GetString(0),
                    Model: reader.GetString(1),
                    Requests: reader.GetInt32(2),
                    PromptTokens: reader.GetInt32(3),
                    CompletionTokens: reader.GetInt32(4),
                    TotalTokens: reader.GetInt32(5),
                    AvgPromptMs: reader.GetDouble(6),
                    AvgCompletionMs: reader.GetDouble(7),
                    CacheHitRate: reader.GetDouble(8)
                ));
            }
            daily = [.. rows];
        }

        // Fetch summary
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = summarySql;
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            AddModelParam(cmd, model);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                summary = new UsageSummaryRow(
                    TotalRequests: reader.GetInt32(0),
                    TotalPromptTokens: reader.GetInt32(1),
                    TotalCompletionTokens: reader.GetInt32(2),
                    TotalTokens: reader.GetInt32(3),
                    AvgPromptMs: reader.GetDouble(4),
                    AvgCompletionMs: reader.GetDouble(5),
                    CacheHitRate: reader.GetDouble(6)
                );
            }
            else
            {
                summary = new(0, 0, 0, 0, 0, 0, 0);
            }
        }

        return new DailyUsageResponse((cutoff, now), summary, daily);
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model)
    {
        await EnsureTableCreatedAsync();

        var countSql = $"""
            SELECT COUNT(*) FROM usage_records
            WHERE 1 = 1
              {GetModelClause(model)}
            """;

        var dataSql = $"""
            SELECT id, timestamp, model, route,
                   prompt_tokens, completion_tokens, total_tokens,
                   prompt_ms, completion_ms, cache_hits,
                   status_code, client_ip
            FROM usage_records
            WHERE 1 = 1
              {GetModelClause(model)}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset
            """;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        // Total count
        long total;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = countSql;
            AddModelParam(cmd, model);
            total = Convert.ToInt64(await cmd.ExecuteScalarAsync()!);
        }

        // Data rows
        UsageRow[] items;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = dataSql;
            AddModelParam(cmd, model);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<UsageRow>();
            while (await reader.ReadAsync())
            {
                rows.Add(new UsageRow(
                    Id: reader.GetInt64(0),
                    Timestamp: DateTimeOffset.Parse(reader.GetString(1)),
                    Model: reader.GetString(2),
                    Route: reader.GetString(3),
                    PromptTokens: reader.GetInt32(4),
                    CompletionTokens: reader.GetInt32(5),
                    TotalTokens: reader.GetInt32(6),
                    PromptMs: reader.IsDBNull(7) ? null : reader.GetDouble(7),
                    CompletionMs: reader.IsDBNull(8) ? null : reader.GetDouble(8),
                    CacheHits: reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    StatusCode: reader.GetInt32(10),
                    ClientIp: reader.IsDBNull(11) ? null : reader.GetString(11)
                ));
            }
            items = [.. rows];
        }

        return new RecentRequestsResponse(total, offset, limit, items);
    }

    /// <summary>
    /// Ensures the usage_records table exists (creates if missing).
    /// </summary>
    private async Task EnsureTableCreatedAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
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
            );
            CREATE INDEX IF NOT EXISTS idx_usage_records_timestamp ON usage_records(timestamp);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Returns the WHERE clause fragment for optional model filtering.
    /// Always starts with AND so it can be safely appended after other WHERE conditions.
    /// Uses parameterized queries to prevent SQL injection.
    /// </summary>
    private static string GetModelClause(string? model) =>
        model is null ? "AND 1 = 1" : "AND model = @model";

    private static void AddModelParam(SqliteCommand cmd, string? model)
    {
        if (model is not null)
            cmd.Parameters.AddWithValue("@model", model);
    }

    public void Dispose()
    {
        // No shared connection to dispose — each query opens its own.
    }
}
