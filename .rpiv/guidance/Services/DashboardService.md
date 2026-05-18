# DashboardService

## Responsibility

Admin endpoint handlers for model control, status queries, and usage statistics. All endpoints are unauthenticated (open to local network).

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IMetaModelManager` | Model lifecycle operations (switch, stop, get active) |
| `IStatsService` | Usage aggregation and cost comparison |
| `IUsageService` | Recent request retrieval |
| `ILogger<DashboardService>` | Structured logging |

## Admin Endpoints

### GET /health

**Purpose:** Router health check + active model name.

**Response:**
```json
{
  "healthy": true,
  "activeModel": "qwen36-smart",
  "backendUrl": "http://localhost:8001"
}
```

**Implementation:**
- Returns `true` if app is running (no backend dependency)
- Includes active model name from `IMetaModelManager.GetActiveModelNameAsync()`

### GET /admin/status

**Purpose:** Current model state and backend URL.

**Response:**
```json
{
  "activeModel": "qwen36-smart",
  "backendUrl": "http://localhost:8001",
  "isIdle": false,
  "processId": 12345
}
```

**Implementation:**
- Queries `IModelRepository` for process state
- Returns `null` fields if no model active

### POST /admin/model

**Purpose:** Switch to specified model.

**Request:**
```json
{ "model": "qwen36-smart" }
```

**Response:** 200 OK or 404 Not Found (if model not configured)

**Implementation:**
- Validates model exists in config
- Calls `ModelManager.EnsureModelAsync()` with optional launch params
- Model auto-starts if not running

### POST /admin/stop

**Purpose:** Stop the currently active model.

**Response:** 200 OK (always succeeds, no-op if idle)

**Implementation:**
- Calls `ModelManager.StopActiveModelAsync()`
- Clears state repository on success
- Logs debug message if no model active

### GET /admin/stats/usage

**Purpose:** Daily aggregated token usage.

**Query params:**
- `days` (optional): Number of days to aggregate (default: 30)
- `model` (optional): Filter by model name

**Response:**
```json
{
  "dailyUsage": [
    {
      "date": "2026-05-18",
      "model": "qwen36-smart",
      "totalTokens": 15000,
      "promptTokens": 5000,
      "completionTokens": 10000,
      "requestCount": 42
    }
  ],
  "totalTokens": 15000,
  "totalRequests": 42
}
```

**Implementation:**
- Queries `SqliteUsagePersistence` via Dapper
- Groups by date + model
- Aggregates token counts and request count

### GET /admin/stats/requests

**Purpose:** Paginated recent requests.

**Query params:**
- `limit` (optional): Max results (default: 50, max: 100)
- `offset` (optional): Pagination offset (default: 0)
- `model` (optional): Filter by model name

**Response:**
```json
{
  "requests": [
    {
      "timestamp": "2026-05-18T14:30:00Z",
      "model": "qwen36-smart",
      "route": "/v1/chat/completions",
      "promptTokens": 150,
      "completionTokens": 300,
      "totalTokens": 450,
      "clientIp": "192.168.1.100",
      "statusCode": 200
    }
  ],
  "totalCount": 1250,
  "limit": 50,
  "offset": 0
}
```

**Implementation:**
- Paginated query via Dapper SQL Builder
- `totalCount` returned for pagination UI
- Includes timing data if available (prompt_ms, completion_ms)

### GET /admin/stats/cost-comparison

**Purpose:** Estimate cloud model costs for same token usage.

**Query params:**
- `days` (optional): Number of days (default: 30)
- `model` (optional): Filter by model name

**Response:**
```json
{
  "tokenUsage": {
    "promptTokens": 5000,
    "completionTokens": 10000,
    "totalTokens": 15000
  },
  "costComparison": [
    {
      "model": "Claude Opus 4.6",
      "estimatedCost": 2.45,
      "currency": "USD"
    },
    {
      "model": "GPT-5.1 Codex Max",
      "estimatedCost": 1.89,
      "currency": "USD"
    }
  ]
}
```

**Implementation:**
- Uses `ModelPricing` for cloud model rates
- Compares against local model token usage
- Lists 6 major cloud models (Claude, GPT, Gemini, Qwen)

## Endpoint Registration Pattern

Endpoints registered via extension method in `Program.cs`:
```csharp
app.MapStatsEndpoints(); // Extension method on WebApplication
```

**Extension method pattern:**
```csharp
public static class StatsEndpointExtensions
{
    public static WebApplication MapStatsEndpoints(this WebApplication app)
    {
        var dashboard = app.Services.GetRequiredService<DashboardService>();
        
        app.MapGet("/health", async () => ...);
        app.MapGet("/admin/status", async () => ...);
        app.MapPost("/admin/model", (HttpContext ctx, ModelSwitchRequest req) => ...);
        // etc.
    }
}
```

## Error Handling

| Scenario | Response |
|----------|----------|
| Model not found in config | 404 Not Found with error JSON |
| Invalid query params | 400 Bad Request |
| Database errors | 500 Internal Server Error |
| No data available | 200 OK with empty arrays |

## Testing Patterns

- **InMemoryModelRepository**: Replace state repository for tests
- **Fake stats service**: Mock `IStatsService` responses
- **Direct method calls**: Test aggregation logic without HTTP layer

See `.rpiv/guidance/LlaModem.Tests/StatsServiceTests.cs` for examples.

## Security Notes

- **All admin endpoints unauthenticated** — accessible to anyone on local network
- **Intended use:** Local development / trusted environment only
- **Auth on proxy routes:** `/v1/*` requires Basic Auth (configurable)

## Performance Considerations

- **Usage aggregation:** Queries SQLite directly — efficient for 30-day windows
- **Recent requests:** Paginated queries prevent large result sets
- **Cost comparison:** Computed in-memory from pricing table — negligible overhead
