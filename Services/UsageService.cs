using Dapper;
using LlaModem.Config;
using LlaModem.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

/// <summary>
/// Persists token usage to a SQLite database.
/// </summary>
public sealed class UsageService : IUsageService, IDisposable
{
    private readonly string _basePath;
    private readonly bool _includeTimings;
    private readonly string _connectionString;

    public UsageService(IOptions<UsageConfig> config)
    {
        var basePath = config.Value.Path;
        _basePath = Path.IsPathRooted(basePath)
            ? basePath
            : Path.Combine(AppContext.BaseDirectory, basePath);
        _includeTimings = config.Value.IncludeTimings;
        _connectionString = $"Data Source={_basePath}";
    }

    public void Record(SessionEntry entry)
    {
        var timings = _includeTimings ? entry.Timings : null;
        var createdStr = entry.Usage.Created?.ToString("o");

        // Ensure the directory exists before opening the connection.
        var dir = Path.GetDirectoryName(_basePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // Ensure schema exists (idempotent, handles migration).
        DbSchema.EnsureAsync(conn).GetAwaiter().GetResult();

        conn.Execute("""
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
                timestamp = entry.Timestamp.ToString("o"),
                requestId = entry.Usage.RequestId,
                created = createdStr,
                model = entry.Model,
                route = entry.Route,
                promptTokens = entry.Usage.PromptTokens,
                completionTokens = entry.Usage.CompletionTokens,
                totalTokens = entry.Usage.TotalTokens,
                promptMs = timings?.PromptMs,
                completionMs = timings?.CompletionMs,
                promptPerTokenMs = timings?.PromptPerTokenMs,
                completionPerTokenMs = timings?.CompletionPerTokenMs,
                cacheHits = timings?.CacheHits,
                cachedTokens = entry.Usage.CachedTokens,
                requestTime = entry.RequestTime.ToString("o"),
                responseTime = entry.ResponseTime.ToString("o"),
                clientIp = entry.ClientIp,
                statusCode = entry.StatusCode,
                requestHeaders = entry.RequestHeaders
            });
    }

    public void Dispose()
    {
        // No state to clean up — connections are per-call.
    }
}
