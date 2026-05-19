---
type: entity
created: 2026-05-18
updated: 2026-05-19
status: complete
---

# StatsService

**Entity Type:** Service  
**Responsibility:** Aggregates usage statistics and provides cost comparison against cloud models. Used by `DashboardService` for admin endpoints `/admin/stats/*`.

## Dependencies

- `IUsageService` - Recent request retrieval
- `ModelPricing` - Cloud model pricing tables (static class)
- `ILogger<StatsService>` - Usage logging

## Key Methods

### GetDailyUsageAsync

```csharp
public async Task<List<DailyUsage>> GetDailyUsageAsync(
    int days = 30, 
    string? model = null,
    CancellationToken ct = default)
{
    var usage = await _usageService.GetDailyAggregationAsync(days, model);
    
    return usage.Select(entry => new DailyUsage {
        Date = entry.Date,
        Model = entry.Model,
        PromptTokens = entry.PromptTokens,
        CompletionTokens = entry.CompletionTokens,
        TotalTokens = entry.TotalTokens,
        RequestCount = entry.RequestCount
    }).ToList();
}
```

**Query:** `session_entries` table grouped by date and model.

### GetRecentRequestsAsync

```csharp
public async Task<PaginatedResponse<SessionEntry>> GetRecentRequestsAsync(
    int limit = 50, 
    int offset = 0, 
    string? model = null)
{
    var entries = await _usageService.GetRecentEntriesAsync(limit, offset, model);
    
    return new PaginatedResponse<SessionEntry> {
        Items = entries.ToList(),
        TotalCount = await _usageService.GetTotalCountAsync(model),
        Limit = limit,
        Offset = offset
    };
}
```

**Query:** `session_entries` table with pagination.

### GetCostComparisonAsync

```csharp
public async Task<CostComparison> GetCostComparisonAsync(
    int days = 30, 
    string? model = null)
{
    var usage = await GetDailyUsageAsync(days, model);
    
    var totalTokens = usage.Sum(u => u.TotalTokens);
    var promptTokens = usage.Sum(u => u.PromptTokens);
    var completionTokens = usage.Sum(u => u.CompletionTokens);

    // Compare against cloud models
    var comparisons = ModelPricing.Models
        .Select(p => new CloudCostEstimate {
            ModelName = p.Name,
            PromptCost = (promptTokens / 1_000_000m) * p.PromptPricePerMillion,
            CompletionCost = (completionTokens / 1_000_000m) * p.CompletionPricePerMillion,
            TotalCost = ...
        })
        .OrderByDescending(c => c.TotalCost);

    return new CostComparison {
        LocalModelUsage = totalTokens,
        CloudEstimates = comparisons.ToList()
    };
}
```

**Pricing tables:** Claude Opus 4.6, Sonnet 4.5, GPT-5.1 Codex Max, Gemini 3 Pro/Flash, Qwen 3 Max.

## Data Models

### DailyUsage Record

```csharp
public sealed record DailyUsage(
    string Date,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int RequestCount);
```

**Source:** Aggregated from `session_entries` table grouped by date and model.

### CostComparison Record

```csharp
public sealed record CostComparison(
    int LocalModelUsage,
    List<CloudCostEstimate> CloudEstimates);

public sealed record CloudCostEstimate(
    string ModelName,
    decimal PromptCost,
    decimal CompletionCost,
    decimal TotalCost);
```

**Calculation:** `(tokens / 1_000_000m) * price_per_million` for prompt and completion separately.

## Endpoint Integration

Registered via `app.MapStatsEndpoints()` in `Program.cs`:

```csharp
var stats = app.Services.GetRequiredService<StatsService>();

// GET /admin/stats/usage
app.MapGet("/admin/stats/usage", async (HttpContext ctx) => 
{
    var days = int.Parse(ctx.Request.Query["days"]);
    var model = ctx.Request.Query["model"];
    var usage = await stats.GetDailyUsageAsync(days, model);
    return Results.Ok(usage);
});

// GET /admin/stats/requests
app.MapGet("/admin/stats/requests", async (HttpContext ctx) => 
{
    var limit = int.Parse(ctx.Request.Query["limit"]);
    var offset = int.Parse(ctx.Request.Query["offset"]);
    var model = ctx.Request.Query["model"];
    var requests = await stats.GetRecentRequestsAsync(limit, offset, model);
    return Results.Ok(requests);
});

// GET /admin/stats/cost-comparison
app.MapGet("/admin/stats/cost-comparison", async (HttpContext ctx) => 
{
    var days = int.Parse(ctx.Request.Query["days"]);
    var model = ctx.Request.Query["model"];
    var comparison = await stats.GetCostComparisonAsync(days, model);
    return Results.Ok(comparison);
});
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Usage service unavailable | Logs error, returns empty list |
| Model not in pricing table | Skips cost comparison for that model |
| Invalid query params | Returns 400 Bad Request |

## Related Entities

- [[entities/UsageService]] - Token usage persistence and retrieval
- [[entities/DashboardService]] - Admin endpoint handlers for stats queries
- [[entities/ConfigRecords]] - ModelPricing static pricing tables

## Related Concepts

- [[concepts/UsageTrackingPattern]] - Usage tracking pattern