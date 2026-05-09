using Dapper;
using LlaModem.Config;
using LlaModem.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

/// <summary>
/// SQLite implementation of IUsagePersistence.
/// Encapsulates all data access logic in one place.
/// </summary>
public sealed class SqliteUsagePersistence : IUsagePersistence, IDisposable
{
    private readonly string _connectionString;
    private readonly bool _includeTimings;

    public SqliteUsagePersistence(IOptions<UsageConfig> config)
    {
        var basePath = config.Value.Path;
        var path = Path.IsPathRooted(basePath)
            ? basePath
            : Path.Combine(AppContext.BaseDirectory, basePath);
        _includeTimings = config.Value.IncludeTimings;
        _connectionString = $"Data Source={path}";
    }

    public async Task AppendAsync(TokenUsage usage, string? model, string? route, DateTimeOffset timestamp, DateTimeOffset requestTime, DateTimeOffset responseTime, string? clientIp, int? statusCode, string? requestHeaders, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_connectionString.Replace("Data Source=", ""));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await DbSchema.EnsureAsync(conn);

        await conn.ExecuteAsync("""
            INSERT INTO usage_records
                (timestamp, request_id, created, model, route, prompt_tokens, completion_tokens, total_tokens,
                 prompt_ms, completion_ms, prompt_per_token_ms, completion_per_token_ms, cache_hits, cached_tokens,
                 request_time, response_time, client_ip, status_code, request_headers)
            VALUES
                (@timestamp, @requestId, @created, @model, @route, @promptTokens, @completionTokens, @totalTokens,
                 @promptMs, @completionMs, @promptPerTokenMs, @completionPerTokenMs, @cacheHits, @cachedTokens,
                 @requestTime, @responseTime, @clientIp, @statusCode, @requestHeaders)
            """, new
            {
                timestamp = timestamp.ToString("o"),
                requestId = usage.RequestId,
                created = usage.Created?.ToString("o"),
                model = model,
                route = route,
                promptTokens = usage.PromptTokens,
                completionTokens = usage.CompletionTokens,
                totalTokens = usage.TotalTokens,
                promptMs = _includeTimings ? usage.PromptMs : (double?)null,
                completionMs = _includeTimings ? usage.CompletionMs : (double?)null,
                promptPerTokenMs = _includeTimings ? usage.PromptPerTokenMs : (double?)null,
                completionPerTokenMs = _includeTimings ? usage.CompletionPerTokenMs : (double?)null,
                cacheHits = _includeTimings ? usage.CacheHits : (int?)null,
                cachedTokens = usage.CachedTokens,
                requestTime = requestTime.ToString("o"),
                responseTime = responseTime.ToString("o"),
                clientIp = clientIp,
                statusCode = statusCode,
                requestHeaders = requestHeaders
            });
    }

    public async Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model = null, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var now = DateTimeOffset.UtcNow;

        var summary = await GetSummaryAsync(conn, days, model, ct) ?? new UsageSummaryRow(0, 0, 0, 0, 0, 0, 0);
        var daily = await GetDailyRowsAsync(conn, days, model, ct);

        return new DailyUsageResponse(new DateRange { From = cutoff.ToString("yyyy-MM-dd"), To = now.ToString("yyyy-MM-dd") }, summary, daily);
    }

    private async Task<UsageSummaryRow?> GetSummaryAsync(SqliteConnection conn, int days, string? model, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var sql = """
            SELECT 
                COUNT(*) as total_requests,
                COALESCE(SUM(prompt_tokens), 0) as total_prompt_tokens,
                COALESCE(SUM(completion_tokens), 0) as total_completion_tokens,
                COALESCE(SUM(total_tokens), 0) as total_tokens,
                CASE WHEN COUNT(*) > 0 THEN AVG(prompt_ms) ELSE 0 END as avg_prompt_ms,
                CASE WHEN COUNT(*) > 0 THEN AVG(completion_ms) ELSE 0 END as avg_completion_ms,
                CASE WHEN COUNT(*) > 0 THEN 
                    COALESCE(AVG(CAST(cache_hits AS REAL) / NULLIF((prompt_tokens + completion_tokens), 0)), 0)
                ELSE 0 END as cache_hit_rate
            FROM usage_records
            WHERE datetime(timestamp) >= datetime('now', '-' || ? || ' days')
            """ + (model is not null ? " AND model = @model" : "");

        var param = new { days, model };
        return await conn.QueryFirstOrDefaultAsync<UsageSummaryRow?>(sql, param);
    }

    private async Task<DailyUsageRow[]> GetDailyRowsAsync(SqliteConnection conn, int days, string? model, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var sql = """
            SELECT 
                DATE(timestamp) as date,
                model,
                COUNT(*) as requests,
                SUM(prompt_tokens) as prompt_tokens,
                SUM(completion_tokens) as completion_tokens,
                SUM(total_tokens) as total_tokens,
                CASE WHEN COUNT(*) > 0 THEN AVG(prompt_ms) ELSE 0 END as avg_prompt_ms,
                CASE WHEN COUNT(*) > 0 THEN AVG(completion_ms) ELSE 0 END as avg_completion_ms,
                CASE WHEN COUNT(*) > 0 THEN 
                    COALESCE(AVG(CAST(cache_hits AS REAL) / NULLIF((prompt_tokens + completion_tokens), 0)), 0)
                ELSE 0 END as cache_hit_rate
            FROM usage_records
            WHERE datetime(timestamp) >= datetime('now', '-' || ? || ' days')
            """ + (model is not null ? " AND model = @model" : "") + @"
            GROUP BY DATE(timestamp), model
            ORDER BY date DESC, model;
            """;

        var param = new { days, model };
        return (await conn.QueryAsync<DailyUsageRow>(sql, param)).ToArray();
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model = null, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT 
                rowid as id,
                timestamp,
                model,
                route,
                prompt_tokens,
                completion_tokens,
                total_tokens,
                prompt_ms,
                completion_ms,
                cache_hits,
                status_code,
                client_ip
            FROM usage_records
            """ + (model is not null ? "WHERE model = @model" : "") + @"
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset;
            """;

        var param = new { limit, offset, model };
        var items = (await conn.QueryAsync<UsageRow>(sql, param)).ToList();
        var total = await GetTotalRequestsAsync(conn, model, ct);

        return new RecentRequestsResponse(total, offset, limit, items.ToArray());
    }

    private async Task<long> GetTotalRequestsAsync(SqliteConnection conn, string? model, CancellationToken ct)
    {
        var sql = """
            SELECT COUNT(*) FROM usage_records
            """ + (model is not null ? "WHERE model = @model" : "");

        return await conn.ExecuteScalarAsync<long>(sql, new { model });
    }

    public void Dispose()
    {
        // No state to clean up — connections are per-call.
    }
}
