using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Thin wrapper around SqliteUsagePersistence for read-only statistics queries.
/// Follows the principle "DRY" — single source of truth for SQL is SqliteUsagePersistence.
/// </summary>
public sealed class StatsService : IStatsService
{
    private readonly IUsagePersistence _persistence;

    public StatsService(IUsagePersistence persistence) => _persistence = persistence;

    public async Task<DailyUsageResponse> GetDailyUsageAsync(int days, string? model)
    {
        return await _persistence.GetDailyUsageAsync(days, model);
    }

    public async Task<RecentRequestsResponse> GetRecentRequestsAsync(int limit, int offset, string? model)
    {
        return await _persistence.GetRecentRequestsAsync(limit, offset, model);
    }
}
