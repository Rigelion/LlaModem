using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Persistent storage for token usage records.
/// Follows the principle "Decouple from implementation details" by abstracting SQLite.
/// </summary>
public interface IUsagePersistence
{
    /// <summary>
    /// Appends a token usage record to storage.
    /// </summary>
    Task AppendAsync(TokenUsage usage, string? model, string? route, DateTimeOffset timestamp, DateTimeOffset requestTime, DateTimeOffset responseTime, string? clientIp, int? statusCode, string? requestHeaders, CancellationToken ct = default);

    /// <summary>
    /// Retrieves daily usage records for a model or all models.
    /// </summary>
    Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves recent request records, optionally filtered by model.
    /// </summary>
    Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model = null, CancellationToken ct = default);
}
