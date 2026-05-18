# UsageService (IUsageService)

## Responsibility

Records token usage from llama-server responses to SQLite database. Parses JSON responses, creates session entries, and persists via Dapper.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IUsagePersistence` | SQLite persistence layer (`SqliteUsagePersistence`) |
| `ModelMetricsService` | In-memory metrics aggregation |
| `ILogger<UsageService>` | Structured logging |

## Data Flow

```
ResponseUsageMiddleware captures response body
  → UsageExtractor.Extract() parses JSON for token usage
  → UsageService.Record(entry) creates SessionEntry
  → SqliteUsagePersistence.Insert() persists to SQLite
  → ModelMetricsService.Update() updates in-memory metrics
```

## SessionEntry Record

```csharp
public record SessionEntry(
    DateTimeOffset Timestamp,
    string Model,
    string Route,
    TokenUsage Usage,
    DateTimeOffset RequestTime,
    DateTimeOffset ResponseTime,
    string? ClientIp,
    int StatusCode,
    IReadOnlyDictionary<string, string> RequestHeaders);

public record TokenUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? PromptMs,
    double? CompletionMs,
    bool? CacheHits);
```

**Notes:**
- `Timestamp`: When request completed (UTC)
- `RequestTime` / `ResponseTime`: Start and end of request processing
- `Usage`: Parsed from llama-server JSON response body
- `RequestHeaders`: Captured Llama headers for debugging

## Recording Pattern

```csharp
public void Record(SessionEntry entry)
{
    // Persist to SQLite
    _persistence.Insert(entry);
    
    // Update in-memory metrics
    _metricsService.RecordUsage(entry.Model, entry.Usage.TotalTokens, entry.RequestTime);
    
    // Log usage summary
    _logger.LogInformation(
        "[USAGE] {Model} {Route} — Prompt: {Prompt}, Completion: {Completion}, Total: {Total}",
        entry.Model, entry.Route, entry.Usage.PromptTokens, 
        entry.Usage.CompletionTokens, entry.Usage.TotalTokens);
}
```

## Usage Capture Middleware Integration

**Middleware:** `ResponseUsageMiddleware` (see `.rpiv/guidance/Middleware/ResponseUsageMiddleware.md`)

**Capture flow:**
1. Non-streaming response detected (`UsageExtractor.IsStreaming()` returns false)
2. Response body buffered in memory
3. JSON parsed for usage stats
4. `SessionEntry` created with timing data
5. `UsageService.Record(entry)` called

**Streaming responses:** Skipped — usage not available until completion.

## SQLite Schema (DbSchema.cs)

```sql
CREATE TABLE IF NOT EXISTS session_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL,
    model TEXT NOT NULL,
    route TEXT NOT NULL,
    prompt_tokens INTEGER NOT NULL,
    completion_tokens INTEGER NOT NULL,
    total_tokens INTEGER NOT NULL,
    request_time TEXT,
    response_time TEXT,
    client_ip TEXT,
    status_code INTEGER,
    prompt_ms REAL,
    completion_ms REAL,
    cache_hits INTEGER
);

CREATE INDEX IF NOT EXISTS idx_session_entries_model ON session_entries(model);
CREATE INDEX IF NOT EXISTS idx_session_entries_timestamp ON session_entries(timestamp);
```

**Dapper queries:** Parameterized SQL in `SqliteUsagePersistence.cs` with `SqlBuilder` for dynamic filters.

## Cost Comparison Implementation

**Pricing table (ModelPricing.cs):**
```csharp
public static class ModelPricing
{
    public static readonly Dictionary<string, decimal> PromptPrices = new()
    {
        ["Claude Opus 4.6"] = 15.0m,   // per 1M tokens
        ["Claude Sonnet 4.5"] = 3.0m,
        ["GPT-5.1 Codex Max"] = 2.5m,
        ["Gemini 3 Pro"] = 3.5m,
        ["Gemini 3 Flash"] = 0.7m,
        ["Qwen 3 Max"] = 1.5m
    };
    
    public static readonly Dictionary<string, decimal> CompletionPrices = new()
    {
        ["Claude Opus 4.6"] = 75.0m,
        ["Claude Sonnet 4.5"] = 9.0m,
        ["GPT-5.1 Codex Max"] = 10.0m,
        ["Gemini 3 Pro"] = 10.5m,
        ["Gemini 3 Flash"] = 2.8m,
        ["Qwen 3 Max"] = 6.0m
    };
}
```

**Calculation:**
```csharp
var promptCost = (promptTokens / 1_000_000m) * ModelPricing.PromptPrices[model];
var completionCost = (completionTokens / 1_000_000m) * ModelPricing.CompletionPrices[model];
return promptCost + completionCost;
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Usage extraction fails (invalid JSON) | Logs warning, skips recording |
| SQLite write fails | Logs error, continues execution |
| Metrics service unavailable | Logs warning, persists anyway |
| Missing model in pricing table | Skips cost comparison for that model |

## Performance Notes

- **Async persistence:** Dapper queries run on background thread pool
- **Buffered writes:** Multiple entries batched per request (one entry per request)
- **Index usage:** Queries use `model` and `timestamp` indexes
- **Memory metrics:** In-memory aggregation avoids repeated DB reads

## Testing Patterns

- **InMemoryUsagePersistence**: Fake persistence for unit tests
- **Mock metrics service**: Verify in-memory updates
- **JSON fixtures**: Test `UsageExtractor` with real llama-server responses

See `.rpiv/guidance/LlaModem.Tests/UsageServiceTests.cs` and `.rpiv/guidance/LlaModem.Tests/UsageCaptureMiddlewareTests.cs`.

## Usage Query Examples

```bash
# Daily usage for last 30 days
sqlite3 usage/usage.db "SELECT date(timestamp) as day, model, SUM(total_tokens) FROM session_entries WHERE timestamp > datetime('now', '-30 days') GROUP BY date(timestamp), model ORDER BY day DESC"

# Recent requests (last 50)
sqlite3 usage/usage.db "SELECT * FROM session_entries ORDER BY timestamp DESC LIMIT 50"

# Model-specific stats
sqlite3 usage/usage.db "SELECT SUM(prompt_tokens), SUM(completion_tokens), COUNT(*) FROM session_entries WHERE model = 'qwen36-smart'"
```

## Workflow: Adding Usage Tracking for New Endpoint

1. Ensure endpoint is under `/v1/*` route (auto-detected by middleware)
2. Verify response is non-streaming (streaming not captured)
3. Confirm llama-server JSON includes `usage` field with token counts
4. Add custom header extraction to `UsageExtractor.CollectLlamaHeaders()` if needed
