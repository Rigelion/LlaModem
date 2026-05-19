# RequestForwarder (IRequestForwarder)

## Responsibility

Proxies HTTP requests from clients to llama-server backends. Handles backend URL selection, request body preservation, and error response formatting.

## Dependencies

| Dependency | Purpose |
|------------|----------|
| `ILogger<RequestForwarder>` | Request/response logging |

## Request Flow

```
Client request (/v1/chat/completions)
  → ModelProxyHandler (select backend URL, load params from dashboard_params.json)
  → RequestForwarder (proxy to backend, capture response)
  → ResponseUsageMiddleware (capture usage stats from response)
```

## Parameter Loading Strategy

**Source**: `dashboard_params.json` (persisted via admin API)

**Flow**:
1. Client sends request with `X-Llama-Model: qwen36-smart`
2. `ModelProxyHandler.LoadParamsForModel()` loads params from file
3. Model starts with loaded parameters (or defaults if file missing)
4. Request body forwarded unchanged to backend

**Important**: No HTTP header-to-body injection occurs — parameters come solely from persisted JSON file.

## Proxy Pattern

```csharp
public async Task<HttpResponseMessage> ForwardAsync(
    HttpContext context, 
    string backendUrl, 
    CancellationToken ct = default)
{
    // Create outgoing request
    var request = new HttpRequestMessage(context.Request.Method, backendUrl);
    
    // Copy headers (excluding hop-by-hop)
    foreach (var header in context.Request.Headers)
        if (!IsHopByHopHeader(header.Key))
            request.Headers.TryAddWithoutValidation(header.Key, header.ToString());
    
    // Copy body
    using var bodyStream = new MemoryStream();
    await context.Request.Body.CopyToAsync(bodyStream);
    bodyStream.Position = 0;
    request.Content = new StreamContent(bodyStream);
    request.Content.Headers.ContentType = context.Request.ContentType;
    
    // Forward to backend
    var response = await _httpClient.SendAsync(request, ct);
    
    // Log response
    _logger.LogInformation("[FORWARD] {Method} {Path} → {BackendUrl} — Status: {Status}",
        context.Request.Method, context.Request.Path, backendUrl, response.StatusCode);
    
    return response;
}
```

## Backend URL Selection

**Source**: `ModelConfig.BackendUrl` (per-model) or `AppConfig.BackendUrl` (default)

```csharp
var backendUrl = modelConfig.BackendUrl ?? _config.BackendUrl;
// Default: "http://localhost:8001"
```

**Notes**:
- All models share port 8001 (sequential execution)
- Per-model override available via `ModelConfig.BackendUrl`
- URL constructed from model name + config

## Error Handling Pattern

**Structured errors**: `ErrorResponseWriter.WriteAsync()` produces consistent JSON:

```json
{
  "error": {
    "code": "BACKEND_UNAVAILABLE",
    "message": "llama-server backend not responding"
  }
}
```

**Error codes (ApiResult<T>)**:
- `MODEL_NOT_FOUND` — Model not in config
- `BACKEND_UNAVAILABLE` — Backend HTTP error / timeout
- `INVALID_REQUEST` — Malformed request body
- `INTERNAL_ERROR` — Unexpected exception

**Implementation**:
```csharp
try
{
    var response = await ForwardAsync(context, backendUrl, ct);
    return response;
}
catch (HttpRequestException ex)
{
    await ErrorResponseWriter.WriteAsync(
        context, 
        "BACKEND_UNAVAILABLE", 
        $"Backend error: {ex.Message}", 
        StatusCodes.Status503);
    throw;
}
```

## Streaming Support

**Status**: Not supported — full response buffered for usage capture.

**Reasoning**:
- Usage stats only available after completion
- llama-server streaming uses SSE format (not JSON)
- Buffering simplifies middleware pipeline

**Impact**: Clients must use non-streaming mode (`stream: false` in request body).

## Content-Type Handling

| Header | Behavior |
|--------|----------|
| `Content-Type: application/json` | Preserved on forwarded request |
| `Accept: application/json` | Passed through to backend |
| Custom headers | Forwarded unless hop-by-hop (Connection, Keep-Alive, etc.) |

## Timeout Configuration

**Source**: `IHttpClientFactory` registered in `Program.cs`:

```csharp
builder.Services.AddHttpClient("ModelManager", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5-minute request timeout
});
```

**Notes**:
- Long-running generation requests supported (up to 5 minutes)
- Health check timeout separate (`HealthCheckTimeoutMinutes`)
- Idle shutdown independent (`IdleTimeoutSeconds`)

## Logging Pattern

**Request logging**: `RequestLoggingMiddleware` (see `.rpiv/guidance/Middleware/RequestLoggingMiddleware.md`)

**Forwarding log**:
```csharp
_logger.LogInformation("[FORWARD] {Method} {Path} → {BackendUrl} — Status: {Status}", ...);
```

**Response body logging**: `ResponseUsageMiddleware` logs full JSON for non-streaming responses.

## Testing Patterns

- **Fake HTTP client**: Mock `HttpClient` responses via `IHttpClientFactory`
- **HttpContext mock**: Use `DefaultHttpContext` for unit tests
- **Error scenarios**: Test backend failures, timeouts, malformed responses

See `.rpiv/guidance/LlaModem.Tests/RequestForwarderTests.cs`.

## Critical Notes

- **No streaming**: Clients must use non-streaming mode for usage tracking
- **No header injection**: Parameters come from `dashboard_params.json` only
- **Body preservation**: Request body forwarded unchanged to backend
- **Timeouts**: 5-minute request timeout — long generations may fail

---

**Date**: 2026-05-19  
**Author**: Rigelion  
**Related Plan**: `.rpiv/artifacts/plans/2026-05-19_14-30-00_remove-header-body-injection.md`
