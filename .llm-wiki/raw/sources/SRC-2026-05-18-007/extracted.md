# RequestLoggingMiddleware

## Responsibility

Logs HTTP request details (method, path, headers, body) at debug level. Used for debugging and troubleshooting request flow.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ILogger<RequestLoggingMiddleware>` | Structured logging |

## Middleware Pipeline Position

```
1. ResponseUsageMiddleware (capture usage stats)
2. RequestLoggingMiddleware (log request details) ← here
3. BasicAuthMiddleware (validate credentials)
4. Endpoint handlers
```

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

**Logged headers:** All request headers except hop-by-hop:
- `X-Llama-Model` — Target model selection
- `X-Llama-*` — Generation parameters (temperature, top_p, etc.)
- `Authorization` — Basic auth credentials (masked in logs)
- `Content-Type`, `Accept` — Content negotiation

**Sensitive data:** `Authorization` header logged as-is (development only — not for production).

## Performance Considerations

- **Debug level only:** No performance impact in production (logs filtered out)
- **Single read:** Body read once, position reset for downstream
- **Async I/O:** `ReadToEndAsync()` avoids blocking
- **Memory:** Full body loaded into string — large payloads may consume memory

## Security Notes

- **Development use:** Logs include full request bodies (not for production)
- **Sensitive headers:** `Authorization` logged unmasked — mask in production config
- **Client IPs:** Logged in usage middleware (`SessionEntry.ClientIp`)

## Testing Patterns

- **HttpContext mock:** Verify debug log calls with correct parameters
- **Body reading:** Test stream reset behavior
- **Header filtering:** Confirm hop-by-hop headers excluded

See `.rpiv/guidance/LlaModem.Tests/RequestForwarderTests.cs` for integration examples.

## Workflow: Debugging Request Flow

1. Enable debug logging in `appsettings.Development.json`:
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Debug",
       "Microsoft": "Warning"
     }
   }
   ```

2. Capture logs via console or file sink:
   ```bash
   dotnet run 2>&1 | grep "\[REQUEST\]"
   ```

3. Correlate with usage logs (`[USAGE]`) and response body logs (`[RESPONSE BODY]`)

## Critical Notes

- **Single pass:** Body read once, position reset — downstream middleware can still access it
- **Debug only:** No performance impact in production (logs filtered)
- **Not for production:** Full request bodies logged — enable only for debugging
