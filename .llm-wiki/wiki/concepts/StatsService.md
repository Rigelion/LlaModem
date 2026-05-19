---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-004]]
status: complete
---

# StatsService

**Description:** Aggregates usage statistics and provides cost comparison against cloud models. Used by `DashboardService` for admin endpoints `/admin/stats/*`.

## Key Methods

### GetDailyUsageAsync
Aggregates token usage from `session_entries` table grouped by date and model. Returns daily usage array with token counts and request count.

### GetRecentRequestsAsync
Paginated recent requests query via Dapper SQL Builder. Returns paginated response with total count for UI pagination support.

### GetCostComparisonAsync
Estimates cloud model costs for same token usage using `ModelPricing` static class. Compares against local model usage and lists 6 major cloud models (Claude, GPT, Gemini, Qwen).

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
- `GET /admin/stats/usage` — Daily aggregated usage
- `GET /admin/stats/requests` — Paginated recent requests
- `GET /admin/stats/cost-comparison` — Cloud cost estimates

## Related Entities

- [[entities/StatsService]] - Usage aggregation service
- [[entities/UsageService]] - Token usage persistence service
- [[entities/DashboardService]] - Admin endpoint handlers
- [[entities/ConfigRecords]] - Configuration records

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
