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

        var summary = await GetSummaryAsync(conn, days, model, ct);
        var daily = await GetDailyRowsAsync(conn, days, model, ct);

        return new DailyUsageResponse(new DateRange { From = cutoff.ToString("yyyy-MM-dd"), To = now.ToString("yyyy-MM-dd") }, summary ?? new UsageSummaryRow(0, 0, 0, 0, 0, 0, 0), daily);
    }

    private async Task<UsageSummaryRow?> GetSummaryAsync(SqliteConnection conn, int days, string? model, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var whereClause = model is not null ? "WHERE timestamp >= @cutoff AND model = @model" : "WHERE timestamp >= @cutoff";
        var sql = $"""
            SELECT 
                COUNT(*) as [TotalRequests],
                COALESCE(SUM(prompt_tokens), 0) as [TotalPromptTokens],
                COALESCE(SUM(completion_tokens), 0) as [TotalCompletionTokens],
                COALESCE(SUM(total_tokens), 0) as [TotalTokens],
                CASE WHEN COUNT(*) > 0 THEN AVG(prompt_ms) ELSE 0 END as [AvgPromptMs],
                CASE WHEN COUNT(*) > 0 THEN AVG(completion_ms) ELSE 0 END as [AvgCompletionMs],
                CASE 
                    WHEN COUNT(*) > 0 THEN COALESCE(CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(*), 0), 0)
                    ELSE 0
                END as [CacheHitRate]
            FROM usage_records
            {whereClause}
            """;

        await DbSchema.EnsureAsync(conn);
        var cutoffStr = cutoff.ToString("o");
        return await conn.QueryFirstOrDefaultAsync<UsageSummaryRow?>(sql, 
            model is not null ? (object)new { cutoff = cutoffStr, model } : (object)new { cutoff = cutoffStr });
    }

    private async Task<DailyUsageRow[]> GetDailyRowsAsync(SqliteConnection conn, int days, string? model, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var whereClause = model is not null ? "WHERE timestamp >= @cutoff AND model = @model" : "WHERE timestamp >= @cutoff";
        var sql = $"""
            SELECT 
                DATE(timestamp) as [Date],
                model as [Model],
                COUNT(*) as [Requests],
                COALESCE(SUM(prompt_tokens), 0) as [PromptTokens],
                COALESCE(SUM(completion_tokens), 0) as [CompletionTokens],
                COALESCE(SUM(total_tokens), 0) as [TotalTokens],
                CASE WHEN COUNT(*) > 0 THEN AVG(prompt_ms) ELSE 0 END as [AvgPromptMs],
                CASE WHEN COUNT(*) > 0 THEN AVG(completion_ms) ELSE 0 END as [AvgCompletionMs],
                CASE 
                    WHEN COUNT(*) > 0 THEN COALESCE(CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / NULLIF(COUNT(*), 0), 0)
                    ELSE 0
                END as [CacheHitRate]
            FROM usage_records
            {whereClause}
            GROUP BY DATE(timestamp), model
            ORDER BY [Date] DESC, model;
            """;

        await DbSchema.EnsureAsync(conn);
        var cutoffStr = cutoff.ToString("o");
        return (await conn.QueryAsync<DailyUsageRow>(sql, 
            model is not null ? (object)new { cutoff = cutoffStr, model } : (object)new { cutoff = cutoffStr })).ToArray();
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model = null, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var whereClause = model is not null ? "WHERE model = @model" : "";
        var sql = $"""
            SELECT 
                rowid as [Id],
                timestamp,
                model as [Model],
                route as [Route],
                prompt_tokens as [PromptTokens],
                completion_tokens as [CompletionTokens],
                total_tokens as [TotalTokens],
                prompt_ms as [PromptMs],
                completion_ms as [CompletionMs],
                cache_hits as [CacheHits],
                status_code as [StatusCode],
                client_ip as [ClientIp]
            FROM usage_records
            {whereClause}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset;
            """;

        var param = model is not null ? new { limit, offset, model } : (object)new { limit, offset };
        await DbSchema.EnsureAsync(conn);
        var items = (await conn.QueryAsync<UsageRow>(sql, param)).ToList();
        var total = await GetTotalRequestsAsync(conn, model, ct);

        return new RecentRequestsResponse(total, offset, limit, items.ToArray());
    }

    private async Task<long> GetTotalRequestsAsync(SqliteConnection conn, string? model, CancellationToken ct)
    {
        var sql = $"""SELECT COUNT(*) FROM usage_records {(model is not null ? "WHERE model = @model" : "")}""";

        await DbSchema.EnsureAsync(conn);
        return await conn.ExecuteScalarAsync<long>(sql, model is not null ? (object)new { model } : (object)null!);
    }

    public void Dispose()
    {
        // No state to clean up — connections are per-call.
    }
}
