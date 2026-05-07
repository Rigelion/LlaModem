using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Reads aggregated and paginated usage statistics from SQLite.
/// </summary>
public interface IStatsService
{
    /// <summary>Get daily aggregated usage for a lookback window.</summary>
    Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model);

    /// <summary>Get paginated recent requests.</summary>
    Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model);
}
