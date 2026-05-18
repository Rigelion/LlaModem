# RequestForwarder

**Entity Type:** Service  
**Responsibility:** Proxies HTTP requests from clients to llama-server backends. Handles header injection, backend URL selection, and error response formatting.

## Dependencies

- `IModelProxyHandler` - Prepares request (injects headers, selects backend)
- `ErrorResponseWriter` - Structured error responses
- `ILogger<RequestForwarder>` - Request/response logging

## Request Flow

1. Client request (`/v1/chat/completions`)
2. `HeaderValueInjector` injects `X-Llama-*` headers to JSON body
3. `ModelProxyHandler` selects backend URL, prepares HttpClient request
4. `RequestForwarder` proxies to backend, captures response
5. `ResponseUsageMiddleware` captures usage stats from response

## Proxy Pattern

```csharp
public async Task<HttpResponseMessage> ForwardAsync(
    HttpContext context, 
    string backendUrl, 
    CancellationToken ct = default)
{
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
    
    var response = await _httpClient.SendAsync(request, ct);
    
    return response;
}
```

## Header Injection Pattern

Configuration: `RouterConfig.BodyHeaderMappings` (e.g., `"x-client-id": "clientId"`). Values auto-typed: boolean, integer, double, or string. Example: `X-Llama-Temperature: 0.7` → body `{ "temperature": 0.7, ... }`.

## Backend URL Selection

Source: `ModelConfig.BackendUrl` (per-model) or `AppConfig.BackendUrl` (default). Default is `http://localhost:8001`. All models share port 8001 (sequential execution).

## Error Handling Pattern

Structured errors via `ErrorResponseWriter.WriteAsync()` with consistent JSON format:
```json
{ "error": { "code": "BACKEND_UNAVAILABLE", "message": "llama-server backend not responding" } }
```

Error codes: `MODEL_NOT_FOUND`, `BACKEND_UNAVAILABLE`, `INVALID_REQUEST`, `INTERNAL_ERROR`.

## Critical Notes

- **No streaming:** Full response buffered for usage capture (streaming not supported)
- **Timeouts:** 5-minute request timeout via `IHttpClientFactory`
- **Header injection:** Only applies to `/v1/*` proxy routes