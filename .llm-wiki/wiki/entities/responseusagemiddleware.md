# ResponseUsageMiddleware

**Entity Type:** Middleware  
**Responsibility:** Captures token usage statistics from non-streaming llama-server responses. Buffers response body once, extracts usage data, persists to SQLite, then restores original body for client delivery.

## Dependencies

- `IUsageService` - Records session entries to persistence
- `ModelMetricsService` - In-memory metrics aggregation
- `ILogger<ResponseUsageMiddleware>` - Usage logging

## Middleware Pipeline Position

1. ResponseUsageMiddleware (capture usage) ← here
2. RequestLoggingMiddleware (log request details)
3. BasicAuthMiddleware (validate credentials)
4. Endpoint handlers

**Note:** Placed early to capture response before any middleware modifies it.

## Streaming Detection

```csharp
public static bool IsStreaming(HttpContext context)
{
    var accept = context.Request.Headers["Accept"].ToString();
    return accept.Contains("text/event-stream") || accept.Contains("application/stream+json");
}
```

**Behavior:**
- Streaming responses: Pass through unchanged (no usage capture)
- Non-streaming: Buffer, parse, record, restore

## Capture Pattern

1. Exit early for streaming requests
2. Buffer response body in MemoryStream
3. Call downstream middleware/handler
4. Log full response body (debug level)
5. Extract usage via `UsageExtractor.Extract()` for proxy routes
6. Create `SessionEntry` with timing data
7. Persist to SQLite via `UsageService.Record(entry)`
8. Restore original body in finally block

## Usage Extraction Pattern

Extractor parses llama-server JSON response:
```json
{
  "choices": [{ "finish_reason": "stop", "text": "...", "usage": { ... } }],
  "usage": {
    "prompt_tokens": 150,
    "completion_tokens": 300,
    "total_tokens": 450,
    "timing": {
      "prompt_ms": 120.5,
      "completion_ms": 450.2,
      "cache_hits": true
    }
  }
}
```

Extraction: `PromptTokens`, `CompletionTokens`, `TotalTokens` from `usage` field. `PromptMs`, `CompletionMs` from `timing` object (optional). `CacheHits` boolean from `timing.cache_hits` (optional).

## Performance Considerations

- Single buffer: Response buffered once, then shared with usage extraction and logging
- Async copy: `CopyToAsync()` avoids blocking thread pool
- Memory efficient: `MemoryStream` reused per request
- No streaming: Streaming responses bypass buffer entirely

## Error Handling

- Usage extraction fails (invalid JSON): Logs warning, continues execution
- SQLite write fails: Logs error, body still restored
- Buffer copy exception: Caught in try-finally, body restored
- Empty response body: Skips usage capture (no-op)