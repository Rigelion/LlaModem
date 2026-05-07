# BE Plan: `/admin/stats/usage` Endpoint

## Goal
Expose the usage data already persisted in SQLite through a clean REST API under `/admin/stats/*`.

## Contract (what the API returns)

### `GET /admin/stats/usage` — Daily aggregated usage

```
Query params:
  days: int (default 30) — lookback window
  model: string? — filter by model name

Response:
{
  "period": { "from": "2026-05-01T00:00:00Z", "to": "2026-05-31T23:59:59Z" },
  "summary": {
    "totalRequests": 1234,
    "totalPromptTokens": 567890,
    "totalCompletionTokens": 123456,
    "totalTokens": 691346,
    "avgPromptMs": 120.5,
    "avgCompletionMs": 89.3,
    "cacheHitRate": 0.34
  },
  "daily": [
    {
      "date": "2026-05-15",
      "requests": 45,
      "model": "qwen-smart",
      "promptTokens": 18000,
      "completionTokens": 4200,
      "totalTokens": 22200,
      "avgPromptMs": 115.2,
      "avgCompletionMs": 92.1,
      "cacheHitRate": 0.28
    }
  ]
}
```

### `GET /admin/stats/requests` — Paginated recent requests

```
Query params:
  model: string? — filter by model
  limit: int (default 50, max 500)
  offset: int (default 0)

Response:
{
  "total": 1234,
  "offset": 0,
  "limit": 50,
  "items": [
    {
      "id": 1,
      "timestamp": "2026-05-15T10:30:00Z",
      "model": "qwen-smart",
      "route": "/v1/chat/completions",
      "promptTokens": 400,
      "completionTokens": 93,
      "totalTokens": 493,
      "promptMs": 120.5,
      "completionMs": 89.3,
      "cacheHits": 12,
      "statusCode": 200,
      "clientIp": "192.168.1.100"
    }
  ]
}
```

## Phases

### Phase 1: Query layer in UsageDbContext
- [x] Create `StatsService` with `GetDailyUsageAsync(int days, string? model)` → returns `DailyUsageRow[]`
- [x] Create `StatsService` with `GetRecentRequestsAsync(int limit, int offset, string? model)` → returns `UsageRow[]`
- [x] Summary aggregation included in `GetDailyUsageAsync` → returns `UsageSummaryRow`
- **Status:** ✅ Done

### Phase 2: Response models (records)
- [x] Define `DailyUsageRow` record struct → `Models/StatsResponse.cs`
- [x] Define `UsageRow` record struct → `Models/StatsResponse.cs`
- [x] Define `UsageSummaryRow` record struct → `Models/StatsResponse.cs`
- [x] Define response DTOs: `DailyUsageResponse`, `RecentRequestsResponse` → `Models/StatsResponse.cs`
- **Status:** ✅ Done

### Phase 3: Endpoint registration
- [x] Register `IStatsService` in `Program.cs` (reads path from `UsageConfig`)
- [x] Add `MapStatsEndpoints()` to `EndpointSetup.cs`
- [x] `GET /admin/stats/usage?days=30&model=xxx` — daily aggregation
- [x] `GET /admin/stats/requests?limit=50&offset=0&model=xxx` — paginated list
- **Status:** ✅ Done

### Phase 4: Build & Test
- [x] `dotnet build` — 0 warnings, 0 errors
- [x] `dotnet test` — 52 passed, 0 failed
- [x] Manual curl test — both `/admin/stats/usage` and `/admin/stats/requests` return valid JSON
- **Status:** ✅ Done

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| `SqliteException: near "1": syntax error` | 1 | Added `AND` prefix to model clause (`AND 1 = 1` instead of `1 = 1`) so SQL is valid after `WHERE timestamp >= @cutoff` |
| `SqliteException: table usage_records does not exist` | 1 | Added `EnsureTableCreatedAsync()` that auto-creates the table and index if missing |
| `InvalidOperationException: Must add values for the following parameters` | 1 | Switched from `?` positional placeholders to named parameters (`@cutoff`, `@model`, `@limit`, `@offset`) matching `AddWithValue` calls |
| `InvalidOperationException: The data is NULL at ordinal 1` (summary) | 1 | Wrapped `SUM()`/`AVG()` in `COALESCE(..., 0)` so empty result sets return 0 instead of NULL |

