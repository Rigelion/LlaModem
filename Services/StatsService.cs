using Dapper;
using LlaModem.Models;
using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// SQLite-backed implementation for usage statistics queries.
/// Uses Dapper for auto-mapping with manual WHERE clause building.
/// </summary>
public sealed class StatsService : IStatsService
{
    private readonly string _connectionString;

    public StatsService(string connectionString) => _connectionString = connectionString;

    public async Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureTableCreatedAsync(conn);

        var daily = await GetDailyRowsAsync(conn, cutoff, model);
        var summary = await GetSummaryAsync(conn, cutoff, model);

        return new DailyUsageResponse((cutoff, now), summary, daily);
    }

    private async Task<DailyUsageRow[]> GetDailyRowsAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var clauses = new List<string> { "WHERE timestamp >= @cutoff" };
        object? parameters = new { cutoff };

        if (model is not null)
        {
            clauses.Add("AND model = @model");
            parameters = new { cutoff, model };
        }

        var sql = $"""
            SELECT date(timestamp) as Date,
                   model as Model,
                   COUNT(*) as Requests,
                   SUM(prompt_tokens) as PromptTokens,
                   SUM(completion_tokens) as CompletionTokens,
                   SUM(total_tokens) as TotalTokens,
                   AVG(prompt_ms) as AvgPromptMs,
                   AVG(completion_ms) as AvgCompletionMs,
                   CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) as CacheHitRate
            FROM usage_records
            {string.Join(" ", clauses)}
            GROUP BY date, model
            ORDER BY date
            """;

        return (await conn.QueryAsync<DailyUsageRow>(sql, parameters)).ToArray();
    }

    private async Task<UsageSummaryRow> GetSummaryAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var clauses = new List<string> { "WHERE timestamp >= @cutoff" };
        object? parameters = new { cutoff };

        if (model is not null)
        {
            clauses.Add("AND model = @model");
            parameters = new { cutoff, model };
        }

        var sql = $"""
            SELECT COUNT(*) as TotalRequests,
                   COALESCE(SUM(prompt_tokens), 0) as TotalPromptTokens,
                   COALESCE(SUM(completion_tokens), 0) as TotalCompletionTokens,
                   COALESCE(SUM(total_tokens), 0) as TotalTokens,
                   COALESCE(AVG(prompt_ms), 0) as AvgPromptMs,
                   COALESCE(AVG(completion_ms), 0) as AvgCompletionMs,
                   CASE WHEN COUNT(*) > 0
                        THEN CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*)
                        ELSE 0
                   END as CacheHitRate
            FROM usage_records
            {string.Join(" ", clauses)}
            """;

        var rows = await conn.QueryAsync<UsageSummaryRow>(sql, parameters);
        return rows.FirstOrDefault();
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureTableCreatedAsync(conn);

        var total = await GetTotalCountAsync(conn, model);
        var items = await GetItemsAsync(conn, limit, offset, model);

        return new RecentRequestsResponse(total, offset, limit, items);
    }

    private async Task<long> GetTotalCountAsync(SqliteConnection conn, string? model)
    {
        object? parameters = model is not null ? (object)new { model } : null;
        var sql = model is not null
            ? "SELECT COUNT(*) FROM usage_records WHERE model = @model"
            : "SELECT COUNT(*) FROM usage_records";

        return await conn.ExecuteScalarAsync<long>(sql!, parameters);
    }

    private async Task<UsageRow[]> GetItemsAsync(
        SqliteConnection conn, int limit, int offset, string? model)
    {
        var clauses = new List<string>();
        object parameters = new { model, limit, offset };

        if (model is not null)
            clauses.Add("WHERE model = @model");

        var sql = $"""
            SELECT id, timestamp, model, route,
                   prompt_tokens as PromptTokens, completion_tokens as CompletionTokens, total_tokens as TotalTokens,
                   prompt_ms as PromptMs, completion_ms as CompletionMs, cache_hits as CacheHits,
                   status_code as StatusCode, client_ip as ClientIp
            FROM usage_records
            {string.Join(" ", clauses)}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset
            """;

        return (await conn.QueryAsync<UsageRow>(sql, parameters)).ToArray();
    }

    private static async Task EnsureTableCreatedAsync(SqliteConnection conn)
    {
        await conn.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS usage_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                request_id TEXT,
                created TEXT,
                model TEXT NOT NULL,
                route TEXT NOT NULL,
                prompt_tokens INTEGER,
                completion_tokens INTEGER,
                total_tokens INTEGER,
                prompt_ms REAL,
                completion_ms REAL,
                prompt_per_token_ms REAL,
                completion_per_token_ms REAL,
                cache_hits INTEGER,
                cached_tokens INTEGER,
                request_time REAL,
                response_time REAL,
                client_ip TEXT,
                status_code INTEGER,
                request_headers TEXT
            )
            """);
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS ix_usage_timestamp ON usage_records(timestamp)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS ix_usage_model ON usage_records(model)");
    }
}
