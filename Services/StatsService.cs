using Dapper;
using LlaModem.Models;
using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// SQLite-backed implementation for usage statistics queries.
/// Uses Dapper for auto-mapping with composable WHERE clauses.
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

        return new DailyUsageResponse(new DateRange { From = cutoff, To = now }, summary, daily);
    }

    private async Task<DailyUsageRow[]> GetDailyRowsAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var where = WhereClause(cutoff, model);
        var sql = $"""
            SELECT date(timestamp) AS Date,
                   model AS Model,
                   COUNT(*) AS Requests,
                   SUM(prompt_tokens) AS PromptTokens,
                   SUM(completion_tokens) AS CompletionTokens,
                   SUM(total_tokens) AS TotalTokens,
                   AVG(prompt_ms) AS AvgPromptMs,
                   AVG(completion_ms) AS AvgCompletionMs,
                   CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) AS CacheHitRate
            FROM usage_records
            {where.Clause}
            GROUP BY date, model
            ORDER BY date
            """;

        return (await conn.QueryAsync<DailyUsageRow>(sql, where.Parameters!)).ToArray();
    }

    private async Task<UsageSummaryRow> GetSummaryAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var where = WhereClause(cutoff, model);
        var sql = $"""
            SELECT COUNT(*) AS TotalRequests,
                   COALESCE(SUM(prompt_tokens), 0) AS TotalPromptTokens,
                   COALESCE(SUM(completion_tokens), 0) AS TotalCompletionTokens,
                   COALESCE(SUM(total_tokens), 0) AS TotalTokens,
                   COALESCE(AVG(prompt_ms), 0) AS AvgPromptMs,
                   COALESCE(AVG(completion_ms), 0) AS AvgCompletionMs,
                   CASE WHEN COUNT(*) > 0
                        THEN CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*)
                        ELSE 0
                   END AS CacheHitRate
            FROM usage_records
            {where.Clause}
            """;

        var rows = await conn.QueryAsync<UsageSummaryRow>(sql, where.Parameters!);
        return rows.FirstOrDefault()!;
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
        var (clause, parameters) = ModelWhereClause(model);
        var sql = $"SELECT COUNT(*) FROM usage_records{clause}";
        return await conn.ExecuteScalarAsync<long>(sql, parameters);
    }

    private async Task<UsageRow[]> GetItemsAsync(
        SqliteConnection conn, int limit, int offset, string? model)
    {
        var sql = model is null
            ? """
                SELECT id, timestamp, model, route,
                       prompt_tokens AS PromptTokens, completion_tokens AS CompletionTokens, total_tokens AS TotalTokens,
                       prompt_ms AS PromptMs, completion_ms AS CompletionMs, cache_hits AS CacheHits,
                       status_code AS StatusCode, client_ip AS ClientIp
                FROM usage_records
                ORDER BY timestamp DESC
                LIMIT @limit OFFSET @offset
                """
            : """
                SELECT id, timestamp, model, route,
                       prompt_tokens AS PromptTokens, completion_tokens AS CompletionTokens, total_tokens AS TotalTokens,
                       prompt_ms AS PromptMs, completion_ms AS CompletionMs, cache_hits AS CacheHits,
                       status_code AS StatusCode, client_ip AS ClientIp
                FROM usage_records
                WHERE model = @model
                ORDER BY timestamp DESC
                LIMIT @limit OFFSET @offset
                """;

        var parameters = model is null
            ? (object)new { limit, offset }
            : new { model, limit, offset };

        return (await conn.QueryAsync<UsageRow>(sql, parameters)).ToArray();
    }

    private static async Task EnsureTableCreatedAsync(SqliteConnection conn)
    {
        await DbSchema.EnsureAsync(conn);
    }

    /// <summary>
    /// Builds a parameterized WHERE clause for queries filtered by timestamp and/or model.
    /// </summary>
    private static (string Clause, object? Parameters) WhereClause(string cutoff, string? model)
    {
        if (model is null)
            return ("WHERE timestamp >= @cutoff", new { cutoff });

        return ("WHERE timestamp >= @cutoff AND model = @model", new { cutoff, model });
    }

    /// <summary>
    /// Builds a parameterized WHERE clause for model-only filtering (no timestamp cutoff).
    /// Returns an empty clause when model is null.
    /// </summary>
    private static (string Clause, object? Parameters) ModelWhereClause(string? model)
    {
        if (model is null)
            return ("", null);

        return (" WHERE model = @model", new { model });
    }
}
