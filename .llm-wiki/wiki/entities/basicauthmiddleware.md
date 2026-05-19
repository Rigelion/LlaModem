# BasicAuthMiddleware

**Entity Type:** Middleware  
**Responsibility:** Validates Basic Auth credentials on `/v1/*` proxy routes. Blocks unauthenticated access to OpenAI-compatible endpoints while leaving admin endpoints (`/admin/*`) open.

## Dependencies

- `IOptions<RouterConfig>` - Auth username/password configuration
- `ILogger<BasicAuthMiddleware>` - Authentication logging

## Middleware Pipeline Position

1. ResponseUsageMiddleware (capture usage stats)
2. RequestLoggingMiddleware (log request details)
3. BasicAuthMiddleware ← here (validate /v1/* routes)
4. Endpoint handlers

**Note:** Applied only to `/v1/*` routes via `app.UseBasicAuthWhen("/v1")`.

## Authentication Pattern

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var path = context.Request.Path.Value ?? string.Empty;
    
    // Only enforce auth on /v1/* routes
    if (!path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase))
    {
        await _next(context);
        return;
    }

    var authHeader = context.Request.Headers["Authorization"].ToString();
    
    if (!IsValidBasicAuth(authHeader))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"LlaModem\"");
        await ErrorResponseWriter.WriteAsync(context, "UNAUTHORIZED", "Invalid credentials", 401);
        return;
    }

    await _next(context); // Auth passed — continue pipeline
}

private static bool IsValidBasicAuth(string authHeader)
{
    if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        return false;

    try
    {
        var credentialBytes = Convert.FromBase64String(authHeader.Substring("Basic ".Length).Trim());
        var credentials = Encoding.UTF8.GetString(credentialBytes);
        
        var parts = credentials.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var username = parts[0];
        var password = parts[1];
        
        return username == _config.AuthUsername && password == _config.AuthPassword;
    }
    catch
    {
        return false;
    }
}
```

## Configuration

Source: `RouterConfig` records with environment variable override:
- `LLAMODEM_AUTH_USERNAME` — Username (preferred over JSON config)
- `LLAMODEM_AUTH_PASSWORD` — Password (preferred over JSON config)

**appsettings.json:**
```json
"Router": {
  "AuthUsername": "admin",
  "AuthPassword": "your-password"
}
```

## Route Filtering

**Applied to:** `/v1/*` routes only:
- `POST /v1/chat/completions` — Auth required
- `POST /v1/completions` — Auth required

**Not applied to:**
- `GET /health` — No auth (public health check)
- `GET /admin/status` — No auth (open admin endpoint)
- `POST /admin/model` — No auth (open model switching)
- `GET /admin/stats/*` — No auth (open usage stats)

## Error Response

**Status code:** 401 Unauthorized  
**WWW-Authenticate header:** `Basic realm="LlaModem"`  
**Body:** Structured JSON error via `ErrorResponseWriter.WriteAsync()`.

## Security Notes

- Basic Auth only: No Bearer tokens, OAuth, or API keys supported
- Plaintext credentials: Base64 encoding — not encryption (requires HTTPS)
- Local network exposure: Admin endpoints unauthenticated — intended for trusted environments only
- Single user: No role-based access control — all authenticated users have full access

## Critical Notes

- Admin endpoints open: `/admin/*` routes bypass auth — security risk on public networks
- No HTTPS: Basic Auth credentials sent in plaintext (use reverse proxy with TLS)
- Password rotation: Requires app restart (config loaded at startup)

## Related Entities

- [[entities/RequestLoggingMiddleware]] - Logs request details before auth check
- [[entities/ResponseUsageMiddleware]] - Captures usage stats after auth passes
- [[entities/ConfigRecords]] - Router configuration (auth credentials)

## Related Concepts

- [[entities/BasicAuthMiddleware]] - HTTP Basic authentication middleware