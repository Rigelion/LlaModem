# UsageTrackingPattern

**Concept Type:** Architecture Pattern  
**Description:** Captures token usage statistics from llama-server responses and persists to SQLite for analytics, cost comparison, and debugging.

## Data Flow

1. **Response captured:** `ResponseUsageMiddleware` buffers response body (non-streaming only)
2. **Usage extracted:** `UsageExtractor.Extract()` parses JSON for token counts and timing data
3. **Session created:** `SessionEntry` created with metadata (model, route, client IP, headers)
4. **Persistence:** `SqliteUsagePersistence.Insert()` writes to SQLite via Dapper
5. **Metrics updated:** `ModelMetricsService.Update()` updates in-memory aggregation

## Data Model

### SessionEntry Record

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

**Fields:**
- `Timestamp`: When request completed (UTC)
- `RequestTime` / `ResponseTime`: Start and end of request processing
- `Usage`: Parsed from llama-server JSON response body
- `RequestHeaders`: Captured Llama headers for debugging

## SQLite Schema

Table: `session_entries` with columns:
- `id`, `timestamp`, `model`, `route`
- `prompt_tokens`, `completion_tokens`, `total_tokens`
- `request_time`, `response_time`
- `client_ip`, `status_code`
- `prompt_ms`, `completion_ms`, `cache_hits`

**Indexes:** On `model` and `timestamp` for efficient queries.

## Usage Capture Middleware Integration

**Middleware:** `ResponseUsageMiddleware` (see [[ResponseUsageMiddleware]])

**Capture flow:**
1. Non-streaming response detected (`UsageExtractor.IsStreaming()` returns false)
2. Response body buffered in memory
3. JSON parsed for usage stats
4. `SessionEntry` created with timing data
5. `UsageService.Record(entry)` called

**Streaming responses:** Skipped — usage not available until completion (SSE format).

## Cost Comparison Implementation

Uses `ModelPricing` static class with pricing tables for cloud models:
- Claude Opus 4.6, Sonnet 4.5
- GPT-5.1 Codex Max
- Gemini 3 Pro, 3 Flash
- Qwen 3 Max

**Calculation:** `(tokens / 1_000_000m) * price_per_million` for prompt and completion separately.

## Query Patterns

### Daily Usage (last 30 days)
```sql
SELECT date(timestamp) as day, model, SUM(total_tokens), COUNT(*) 
FROM session_entries 
WHERE timestamp > datetime('now', '-30 days') 
GROUP BY date(timestamp), model 
ORDER BY day DESC
```

### Recent Requests (paginated)
```sql
SELECT * FROM session_entries 
WHERE model = 'qwen36-smart' 
ORDER BY timestamp DESC 
LIMIT 50 OFFSET 0
```

### Model-Specific Stats
```sql
SELECT SUM(prompt_tokens), SUM(completion_tokens), COUNT(*) 
FROM session_entries 
WHERE model = 'qwen36-smart'
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Usage extraction fails (invalid JSON) | Logs warning, skips recording |
| SQLite write fails | Logs error, continues execution |
| Metrics service unavailable | Logs warning, persists anyway |
| Missing model in pricing table | Skips cost comparison for that model |

## Performance Considerations

- **Async persistence:** Dapper queries run on background thread pool
- **Buffered writes:** One entry per request (not batched)
- **Index usage:** Queries use `model` and `timestamp` indexes
- **Memory metrics:** In-memory aggregation avoids repeated DB reads

## Related Entities

- [[UsageService]]
- [[ResponseUsageMiddleware]]
- [[UsageExtractor]]
- [[StatsService]] (not documented yet)