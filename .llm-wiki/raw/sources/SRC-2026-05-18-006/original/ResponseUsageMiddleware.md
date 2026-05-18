# ResponseUsageMiddleware

## Responsibility

Captures token usage statistics from non-streaming llama-server responses. Buffers response body once, extracts usage data, persists to SQLite, then restores original body for client delivery.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IUsageService` | Records session entries to persistence |
| `ModelMetricsService` | In-memory metrics aggregation |
| `ILogger<ResponseUsageMiddleware>` | Usage logging |

## Middleware Pipeline Position

```
1. ResponseUsageMiddleware (capture usage)
2. RequestLoggingMiddleware (log request details)
3. BasicAuthMiddleware (validate credentials)
4. Endpoint handlers
```

**Note:** Placed early to capture response before any middleware modifies it.

## Streaming Detection

```csharp
public static bool IsStreaming(HttpContext context)
{
    // Check Accept header for SSE content type
    var accept = context.Request.Headers["Accept"].ToString();
    return accept.Contains("text/event-stream") || accept.Contains("application/stream+json");
}
```

**Behavior:**
- Streaming responses: Pass through unchanged (no usage capture)
- Non-streaming: Buffer, parse, record, restore

## Capture Pattern

```csharp
public async Task InvokeAsync(HttpContext context)
{
    // Exit early for streaming
    if (UsageExtractor.IsStreaming(context))
    {
        await _next(context);
        return;
    }

    var requestTime = DateTimeOffset.UtcNow;
    var originalBody = context.Response.Body;
    
    // Buffer response body
    using var buffer = new MemoryStream();
    context.Response.Body = buffer;

    try
    {
        await _next(context); // Call downstream middleware/handler
        
        var responseTime = DateTimeOffset.UtcNow;
        buffer.Seek(0, SeekOrigin.Begin);
        
        // Log full response body (debug)
        var bytes = buffer.ToArray();
        if (bytes.Length > 0)
        {
            var bodyString = Encoding.UTF8.GetString(bytes);
            _logger.LogInformation("[RESPONSE BODY] {Method} {Path}\n{Body}", ...);
        }

        // Extract usage for proxy routes
        var path = context.Request.Path.Value ?? string.Empty;
        var isProxyRoute = path.StartsWith("/v1/chat/completions") 
                        || path.StartsWith("/v1/completions");

        if (isProxyRoute && !string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(bytes)))
        {
            var raw = Encoding.UTF8.GetString(bytes);
            var usage = UsageExtractor.Extract(raw); // TokenUsage?

            if (usage is not null)
            {
                var model = UsageExtractor.ExtractModel(raw) ?? "(unknown)";
                var entry = new SessionEntry(
                    Timestamp: DateTimeOffset.UtcNow,
                    Model: model,
                    Route: path,
                    Usage: usage with { RequestTime, ResponseTime },
                    ClientIp: context.Connection.RemoteIpAddress?.ToString(),
                    StatusCode: context.Response.StatusCode,
                    RequestHeaders: UsageExtractor.CollectLlamaHeaders(context.Request.Headers));

                _usageService.Record(entry); // Persists to SQLite
                
                _metricsService?.RecordUsage(model, usage.TotalTokens, entry.RequestTime);
            }
        }
    }
    finally
    {
        // Restore original body for client delivery
        await RestoreOriginalBodyAsync(buffer, originalBody);
    }
}
```

## Usage Extraction Pattern

**Extractor:** `UsageExtractor.Extract(string json)` parses llama-server JSON:

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

**Extraction:**
- `PromptTokens`, `CompletionTokens`, `TotalTokens` from `usage` field
- `PromptMs`, `CompletionMs` from `timing` object (optional)
- `CacheHits` boolean from `timing.cache_hits` (optional)

## Body Restoration Pattern

```csharp
private static async Task RestoreOriginalBodyAsync(MemoryStream buffer, Stream originalBody)
{
    buffer.Seek(0, SeekOrigin.Begin);
    await buffer.CopyToAsync(originalBody);
}
```

**Purpose:** Ensure client receives unmodified response after usage capture.

**Try-finally:** Always restores body even on exception (prevents client hangs).

## Logging Output

**Usage log (structured):**
```log
[USAGE] qwen36-smart /v1/chat/completions — Prompt: 150, Completion: 300, Total: 450, Prompt: 120.5ms, Completion: 450.2ms
```

**Response body log (debug):**
```log
[RESPONSE BODY] POST /v1/chat/completions
{
  "choices": [...],
  "usage": { ... }
}
```

## Performance Considerations

- **Single buffer:** Response buffered once, then shared with usage extraction and logging
- **Async copy:** `CopyToAsync()` avoids blocking thread pool
- **Memory efficient:** `MemoryStream` reused per request (GC-friendly)
- **No streaming:** Streaming responses bypass buffer entirely

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Usage extraction fails (invalid JSON) | Logs warning, continues execution |
| SQLite write fails | Logs error, body still restored |
| Buffer copy exception | Caught in try-finally, body restored |
| Empty response body | Skips usage capture (no-op) |

## Testing Patterns

- **HttpContext mock:** `DefaultHttpContext` with MemoryStream body
- **Fake usage service:** Verify recording calls with correct parameters
- **Streaming detection:** Test various Accept header values

See `.rpiv/guidance/LlaModem.Tests/UsageCaptureMiddlewareTests.cs`.

## Workflow: Adding Usage Tracking for New Endpoint

1. Ensure endpoint path matches `/v1/*` prefix (auto-detected)
2. Verify response is non-streaming JSON (not SSE)
3. Confirm llama-server includes `usage` field in response
4. Add custom header extraction to `UsageExtractor.CollectLlamaHeaders()` if needed

## Critical Notes

- **Streaming not supported:** Usage stats unavailable for SSE responses
- **Buffer size:** Limited by available memory — very large responses may fail
- **Proxy routes only:** Admin endpoints (`/admin/*`) bypass usage capture
- **Model extraction:** Falls back to `(unknown)` if model header missing
