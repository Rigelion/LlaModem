using LlaModem.Config;
using LlaModem.Models;
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

    private UsageDbContext CreateContext()
    {
        var dir = Path.GetDirectoryName(_basePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        return new UsageDbContext(_connectionString);
    }

    public void Record(SessionEntry entry)
    {
        using var ctx = CreateContext();
        ctx.EnsureCreated();

        var timings = _includeTimings ? entry.Timings : null;

        ctx.Insert(
            timestamp: entry.Timestamp.ToString("o"),
            model: entry.Model,
            route: entry.Route,
            promptTokens: entry.Usage.PromptTokens,
            completionTokens: entry.Usage.CompletionTokens,
            totalTokens: entry.Usage.TotalTokens,
            promptMs: timings?.PromptMs,
            completionMs: timings?.CompletionMs,
            promptPerTokenMs: timings?.PromptPerTokenMs,
            completionPerTokenMs: timings?.CompletionPerTokenMs,
            cacheHits: timings?.CacheHits);
    }

    public void Dispose()
    {
        // Contexts are disposed per-call; nothing to clean up.
    }
}
