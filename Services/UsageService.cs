using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Adapts IUsagePersistence to IUsageService for compatibility.
/// </summary>
public sealed class UsageService : IUsageService, IDisposable
{
    private readonly IUsagePersistence _persistence;

    public UsageService(IUsagePersistence persistence)
    {
        _persistence = persistence;
    }

    public void Record(SessionEntry entry)
    {
        var usage = new TokenUsage(
            entry.Usage.PromptTokens,
            entry.Usage.CompletionTokens,
            entry.Usage.TotalTokens,
            entry.Timings?.PromptMs,
            entry.Timings?.CompletionMs,
            entry.Timings?.PromptPerTokenMs,
            entry.Timings?.CompletionPerTokenMs,
            entry.Timings?.CacheHits,
            entry.Usage.RequestId,
            entry.Usage.Created,
            entry.Usage.CachedTokens);

        _persistence.AppendAsync(usage, entry.Model, entry.Route, entry.Timestamp, entry.RequestTime, entry.ResponseTime, entry.ClientIp, entry.StatusCode, entry.RequestHeaders);
    }

    public void Dispose()
    {
        // No state to clean up — persistence is injected.
    }
}
