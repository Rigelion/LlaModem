# RequestLoggingMiddleware

**Entity Type:** Middleware  
**Responsibility:** Logs HTTP request details (method, path, headers, body) at debug level. Used for debugging and troubleshooting request flow.

## Dependencies

- `ILogger<RequestLoggingMiddleware>` - Structured logging

## Middleware Pipeline Position

1. ResponseUsageMiddleware (capture usage stats)
2. RequestLoggingMiddleware ← here (log request details)
3. BasicAuthMiddleware (validate credentials)
4. Endpoint handlers

**Note:** Placed after `ResponseUsageMiddleware` so response body is available for logging.

## Logging Pattern

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var request = context.Request;
    
    _logger.LogDebug(
        "[REQUEST] {Method} {Path}\nHeaders: {Headers}\nBody: {Body}",
        request.Method,
        request.Path,
        string.Join("; ", request.Headers.Select(h => $"{h.Key}={h.Value}")),
        await ReadBodyAsString(request.Body));
    
    await _next(context);
}

private static async Task<string> ReadBodyAsString(Stream body)
{
    using var reader = new StreamReader(body, Encoding.UTF8, leaveOpen: true);
    var bodyString = await reader.ReadToEndAsync();
    body.Position = 0; // Reset for downstream middleware
    return bodyString;
}
```

## Log Output Format

**Debug level:**
```log
[REQUEST] POST /v1/chat/completions
Headers: X-Llama-Model=qwen36-smart, X-Llama-Temperature=0.7, Content-Type=application/json
Body: {"model":"qwen36-smart","messages":[{"role":"user","content":"Hello"}],"temperature":0.7}
```

**Response logging:** Handled by `ResponseUsageMiddleware` (separate log entry).

## Body Reading Pattern

**Key considerations:**
- Stream must be seekable or rewound after reading
- `leaveOpen: true` prevents closing original stream
- Position reset allows downstream middleware to read body

**Alternative:** Use `Request.Body.EnableBuffering()` for multiple reads (not used here — single pass).

## Header Logging

Logged headers: All request headers except hop-by-hop. Includes `X-Llama-Model`, `X-Llama-*` generation parameters, `Authorization` (masked in logs), `Content-Type`, `Accept`.

**Sensitive data:** `Authorization` header logged as-is (development only — not for production).

## Performance Considerations

- Debug level only: No performance impact in production (logs filtered out)
- Single read: Body read once, position reset for downstream
- Async I/O: `ReadToEndAsync()` avoids blocking
- Memory: Full body loaded into string — large payloads may consume memory

## Security Notes

- Development use: Logs include full request bodies (not for production)
- Sensitive headers: `Authorization` logged unmasked — mask in production config
- Client IPs: Logged in usage middleware (`SessionEntry.ClientIp`)