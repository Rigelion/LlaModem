# RequestForwarder (IRequestForwarder)

## Responsibility

Proxies HTTP requests from clients to llama-server backends. Handles header injection, backend URL selection, and error response formatting.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IModelProxyHandler` | Prepares request (injects headers, selects backend) |
| `ErrorResponseWriter` | Structured error responses |
| `ILogger<RequestForwarder>` | Request/response logging |

## Request Flow

```
Client request (/v1/chat/completions)
  → HeaderValueInjector (inject X-Llama-* headers to JSON body)
  → ModelProxyHandler (select backend URL, prepare HttpClient request)
  → RequestForwarder (proxy to backend, capture response)
  → ResponseUsageMiddleware (capture usage stats from response)
```

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

## Header Injection Pattern

**Location:** `HeaderValueInjector` (see `.rpiv/guidance/Services/HeaderValueInjector.md`)

**Configuration:** `RouterConfig.BodyHeaderMappings`:
```json
"BodyHeaderMappings": {
  "x-client-id": "clientId",
  "x-debug": "debug"
}
```

**Behavior:**
- Reads HTTP headers before forwarding
- Injects values at JSON root level
- Auto-typed: boolean, integer, double, or string
- Example: `X-Llama-Temperature: 0.7` → body `{ "temperature": 0.7, ... }`

## Backend URL Selection

**Source:** `ModelConfig.BackendUrl` (per-model) or `AppConfig.BackendUrl` (default)

```csharp
var backendUrl = modelConfig.BackendUrl ?? _config.BackendUrl;
// Default: "http://localhost:8001"
```

**Notes:**
- All models share port 8001 (sequential execution)
- Per-model override available via `ModelConfig.BackendUrl`
- URL constructed from model name + config

## Error Handling Pattern

**Structured errors:** `ErrorResponseWriter.WriteAsync()` produces consistent JSON:

```json
{
  "error": {
    "code": "BACKEND_UNAVAILABLE",
    "message": "llama-server backend not responding"
  }
}
```

**Error codes (ApiResult<T>):**
- `MODEL_NOT_FOUND` — Model not in config
- `BACKEND_UNAVAILABLE` — Backend HTTP error / timeout
- `INVALID_REQUEST` — Malformed request body
- `INTERNAL_ERROR` — Unexpected exception

**Implementation:**
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

**Status:** Not supported — full response buffered for usage capture.

**Reasoning:**
- Usage stats only available after completion
- llama-server streaming uses SSE format (not JSON)
- Buffering simplifies middleware pipeline

**Impact:** Clients must use non-streaming mode (`stream: false` in request body).

## Content-Type Handling

| Header | Behavior |
|--------|----------|
| `Content-Type: application/json` | Preserved on forwarded request |
| `Accept: application/json` | Passed through to backend |
| Custom headers | Forwarded unless hop-by-hop (Connection, Keep-Alive, etc.) |

## Timeout Configuration

**Source:** `IHttpClientFactory` registered in `Program.cs`:

```csharp
builder.Services.AddHttpClient("ModelManager", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5-minute request timeout
});
```

**Notes:**
- Long-running generation requests supported (up to 5 minutes)
- Health check timeout separate (`HealthCheckTimeoutMinutes`)
- Idle shutdown independent (`IdleTimeoutSeconds`)

## Logging Pattern

**Request logging:** `RequestLoggingMiddleware` (see `.rpiv/guidance/Middleware/RequestLoggingMiddleware.md`)

**Forwarding log:**
```csharp
_logger.LogInformation("[FORWARD] {Method} {Path} → {BackendUrl} — Status: {Status}", ...);
```

**Response body logging:** `ResponseUsageMiddleware` logs full JSON for non-streaming responses.

## Testing Patterns

- **Fake HTTP client**: Mock `HttpClient` responses via `IHttpClientFactory`
- **HttpContext mock**: Use `DefaultHttpContext` for unit tests
- **Error scenarios**: Test backend failures, timeouts, malformed responses

See `.rpiv/guidance/LlaModem.Tests/RequestForwarderTests.cs`.

## Workflow: Adding New Header Mapping

1. Update `appsettings.json`:
   ```json
   "BodyHeaderMappings": {
     "x-new-header": "newField"
   }
   ```

2. Values auto-typed by `HeaderValueInjector` (no code change needed)

3. Test with curl:
   ```bash
   curl -H "X-New-Header: value" http://localhost:9000/v1/chat/completions
   ```

4. Verify injected in request body via logs or network capture

## Critical Notes

- **No streaming**: Clients must use non-streaming mode for usage tracking
- **Header injection**: Only applies to `/v1/*` proxy routes (not admin endpoints)
- **Body modification**: Injected fields appear at JSON root level, not nested
- **Timeouts**: 5-minute request timeout — long generations may fail
