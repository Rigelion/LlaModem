# UsageService

**Entity Type:** Service  
**Responsibility:** Records token usage from llama-server responses to SQLite database. Parses JSON responses, creates session entries, and persists via Dapper.

## Dependencies

- `IUsagePersistence` - SQLite persistence layer (`SqliteUsagePersistence`)
- `ModelMetricsService` - In-memory metrics aggregation
- `ILogger<UsageService>` - Structured logging

## Data Flow

1. `ResponseUsageMiddleware` captures response body
2. `UsageExtractor.Extract()` parses JSON for token usage
3. `UsageService.Record(entry)` creates SessionEntry
4. `SqliteUsagePersistence.Insert()` persists to SQLite
5. `ModelMetricsService.Update()` updates in-memory metrics

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

## SQLite Schema

Table: `session_entries` with columns for timestamp, model, route, token counts, timing data, client IP, status code. Indexes on `model` and `timestamp`.

## Cost Comparison Implementation

Uses `ModelPricing` static class with pricing tables for cloud models (Claude Opus/Sonnet, GPT-5.1 Codex Max, Gemini 3 Pro/Flash, Qwen 3 Max). Calculates cost as `(tokens / 1_000_000m) * price_per_million`.

## Error Handling
- Usage extraction fails (invalid JSON): Logs warning, skips recording
- SQLite write fails: Logs error, continues execution
- Metrics service unavailable: Logs warning, persists anyway