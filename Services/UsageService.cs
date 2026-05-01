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
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public UsageService(IOptions<UsageConfig> config)
    {
        var basePath = config.Value.Path;
        _basePath = Path.IsPathRooted(basePath)
            ? basePath
            : Path.Combine(AppContext.BaseDirectory, basePath);
        _filenamePattern = config.Value.FilenamePattern;
    }

    public void Record(SessionEntry entry)
    {
        var dateStr = entry.Timestamp.ToString("yyyy-MM-dd");
        var fileName = _filenamePattern.Replace("{date}", dateStr);
        var filePath = Path.Combine(_basePath, fileName);

        var line = FormatLine(entry);

        _semaphore.Wait();
        try
        {
            Directory.CreateDirectory(_basePath);

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, FormatHeader(dateStr));
            }

            File.AppendAllText(filePath, line);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string FormatHeader(string date) =>
        $"# Usage Report — {date}\n\n" +
        "| Timestamp | Model | Route | Prompt Tokens | Completion Tokens | Total Tokens |\n" +
        "|-----------|-------|-------|---------------|-------------------|--------------|\n";

    private static string FormatLine(SessionEntry entry) =>
        $"| {entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {entry.Model} | {entry.Route} | {entry.Usage.PromptTokens} | {entry.Usage.CompletionTokens} | {entry.Usage.TotalTokens} |\n";

    public void Dispose() => _semaphore.Dispose();
}
