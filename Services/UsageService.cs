using LlaModem.Config;
using LlaModem.Models;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

/// <summary>
/// Persists token usage to daily markdown files.
/// One file per day: usage-2026-05-01.md
/// </summary>
public sealed class UsageService : IUsageService, IDisposable
{
    private readonly string _basePath;
    private readonly string _filenamePattern;
    private readonly UsageConfig _config;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public UsageService(IOptions<UsageConfig> config)
    {
        var basePath = config.Value.Path;
        _basePath = Path.IsPathRooted(basePath)
            ? basePath
            : Path.Combine(AppContext.BaseDirectory, basePath);
        _filenamePattern = config.Value.FilenamePattern;
        _config = config.Value;
    }

    public void Record(SessionEntry entry)
    {
        var dateStr = entry.Timestamp.ToString("yyyy-MM-dd");
        var fileName = _filenamePattern.Replace("{date}", dateStr);
        var filePath = Path.Combine(_basePath, fileName);

        var includeTimings = _config.IncludeTimings;
        var line = FormatLine(entry, includeTimings);

        _semaphore.Wait();
        try
        {
            Directory.CreateDirectory(_basePath);

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, FormatHeader(dateStr, includeTimings));
            }

            File.AppendAllText(filePath, line);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private string FormatHeader(string date, bool includeTimings)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"# Usage Report — {date}\n\n");
        sb.Append("| Timestamp | Model | Route | Prompt Tokens | Completion Tokens | Total Tokens |");

        if (includeTimings)
        {
            sb.Append(" | Prompt Ms | Completion Ms | Cache Hits");
        }

        sb.Append("\n");
        sb.Append("|-----------|-------|-------|---------------|-------------------|--------------|");

        if (includeTimings)
        {
            sb.Append(" |-----------|-------------|------------|");
        }

        return sb.ToString();
    }

    private static string FormatLine(SessionEntry entry, bool includeTimings)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"| {entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {entry.Model} | {entry.Route} | {entry.Usage.PromptTokens} | {entry.Usage.CompletionTokens} | {entry.Usage.TotalTokens} |");

        if (includeTimings && entry.Timings is { } t)
        {
            var promptMs = t.PromptMs.HasValue && !double.IsNaN(t.PromptMs.Value) ? $"{t.PromptMs.Value:F1}" : "";
            var completionMs = t.CompletionMs.HasValue && !double.IsNaN(t.CompletionMs.Value) ? $"{t.CompletionMs.Value:F1}" : "";
            var cacheHits = t.CacheHits?.ToString() ?? "";
            sb.Append($" | {promptMs} | {completionMs} | {cacheHits}");
        }

        sb.Append("|\n");
        return sb.ToString();
    }

    public void Dispose() => _semaphore.Dispose();
}
