namespace LlaModem.Models;

/// <summary>
/// Aggregated usage for a single day (one model).
/// </summary>
public readonly record struct DailyUsageRow(
    string Date,
    string Model,
    int Requests,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double AvgPromptMs,
    double AvgCompletionMs,
    double CacheHitRate);

/// <summary>
/// Single usage record for the paginated requests list.
/// </summary>
public readonly record struct UsageRow(
    long Id,
    DateTimeOffset Timestamp,
    string Model,
    string Route,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? PromptMs,
    double? CompletionMs,
    int? CacheHits,
    int StatusCode,
    string? ClientIp);

/// <summary>
/// Overall summary across all days and models.
/// </summary>
public readonly record struct UsageSummaryRow(
    int TotalRequests,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    int TotalTokens,
    double AvgPromptMs,
    double AvgCompletionMs,
    double CacheHitRate);

/// <summary>
/// Response envelope for daily usage aggregation.
/// </summary>
public sealed record DailyUsageResponse(
    (string From, string To) Period,
    UsageSummaryRow Summary,
    DailyUsageRow[] Daily);

/// <summary>
/// Response envelope for paginated recent requests.
/// </summary>
public sealed record RecentRequestsResponse(
    long Total,
    int Offset,
    int Limit,
    UsageRow[] Items);
