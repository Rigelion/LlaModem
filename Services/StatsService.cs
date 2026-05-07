using Dapper;
using LlaModem.Models;
using Microsoft.Data.Sqlite;

namespace LlaModem.Services;

/// <summary>
/// SQLite-backed implementation for usage statistics queries.
/// Uses Dapper for auto-mapping and SqlBuilder for dynamic WHERE clauses.
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

        var daily = await GetDailyRowsAsync(conn, cutoff, model);
        var summary = await GetSummaryAsync(conn, cutoff, model);

        return new DailyUsageResponse((cutoff, now), summary, daily);
    }

    private async Task<DailyUsageRow[]> GetDailyRowsAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var builder = new SqlBuilder();
        var where = builder.AddTemplate("/**where**/");

        builder.Where("timestamp >= @cutoff", new { cutoff });
        if (model is not null)
            builder.Where("model = @model", new { model });

        var sql = $"""
            SELECT date(timestamp) as date,
                   model,
                   COUNT(*) as requests,
                   SUM(prompt_tokens) as prompt_tokens,
                   SUM(completion_tokens) as completion_tokens,
                   SUM(total_tokens) as total_tokens,
                   AVG(prompt_ms) as avg_prompt_ms,
                   AVG(completion_ms) as avg_completion_ms,
                   CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) as cache_hit_rate
            FROM usage_records
            /**where**/
            GROUP BY date, model
            ORDER BY date
            """;

        return (await conn.QueryAsync<DailyUsageRow>(sql, new { cutoff, model })).ToArray();
    }

    private async Task<UsageSummaryRow> GetSummaryAsync(
        SqliteConnection conn, string cutoff, string? model)
    {
        var builder = new SqlBuilder();
        var where = builder.AddTemplate("/**where**/");

        builder.Where("timestamp >= @cutoff", new { cutoff });
        if (model is not null)
            builder.Where("model = @model", new { model });

        var sql = $"""
            SELECT COUNT(*) as total_requests,
                   COALESCE(SUM(prompt_tokens), 0) as total_prompt_tokens,
                   COALESCE(SUM(completion_tokens), 0) as total_completion_tokens,
                   COALESCE(SUM(total_tokens), 0) as total_tokens,
                   COALESCE(AVG(prompt_ms), 0) as avg_prompt_ms,
                   COALESCE(AVG(completion_ms), 0) as avg_completion_ms,
                   CASE WHEN COUNT(*) > 0
                        THEN CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*)
                        ELSE 0
                   END as cache_hit_rate
            FROM usage_records
            /**where**/
            """;

        var row = await conn.QueryFirstOrDefaultAsync<UsageSummaryRow?>(sql, new { cutoff, model });
        return row ?? new UsageSummaryRow(0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var total = await GetTotalCountAsync(conn, model);
        var items = await GetItemsAsync(conn, limit, offset, model);

        return new RecentRequestsResponse(total, offset, limit, items);
    }

    private async Task<long> GetTotalCountAsync(SqliteConnection conn, string? model)
    {
        var builder = new SqlBuilder();
        var where = builder.AddTemplate("/**where**/");

        if (model is not null)
            builder.Where("model = @model", new { model });

        var sql = $"""
            SELECT COUNT(*) FROM usage_records
            /**where**/
            """;

        return await conn.ExecuteScalarAsync<long>(sql, new { model });
    }

    private async Task<UsageRow[]> GetItemsAsync(
        SqliteConnection conn, int limit, int offset, string? model)
    {
        var builder = new SqlBuilder();
        var where = builder.AddTemplate("/**where**/");
        var orderby = builder.AddTemplate("/**orderby**/");

        if (model is not null)
            builder.Where("model = @model", new { model });
        builder.OrderBy("timestamp DESC");

        var sql = $"""
            SELECT id, timestamp, model, route,
                   prompt_tokens, completion_tokens, total_tokens,
                   prompt_ms, completion_ms, cache_hits,
                   status_code, client_ip
            FROM usage_records
            /**where**/
            /**orderby**/
            LIMIT @limit OFFSET @offset
            """;

        return (await conn.QueryAsync<UsageRow>(sql, new { model, limit, offset })).ToArray();
    }
}
