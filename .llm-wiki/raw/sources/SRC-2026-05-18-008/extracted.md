# BasicAuthMiddleware

## Responsibility

Validates Basic Auth credentials on `/v1/*` proxy routes. Blocks unauthenticated access to OpenAI-compatible endpoints while leaving admin endpoints (`/admin/*`) open.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IOptions<RouterConfig>` | Auth username/password configuration |
| `ILogger<BasicAuthMiddleware>` | Authentication logging |

## Middleware Pipeline Position

```
1. ResponseUsageMiddleware (capture usage stats)
2. RequestLoggingMiddleware (log request details)
3. BasicAuthMiddleware ← here (validate /v1/* routes)
4. Endpoint handlers
```

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
        await ErrorResponseWriter.WriteAsync(
            context, 
            "UNAUTHORIZED", 
            "Invalid credentials", 
            StatusCodes.Status401);
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
        var credentialBytes = Convert.FromBase64String(
            authHeader.Substring("Basic ".Length).Trim());
        var credentials = Encoding.UTF8.GetString(credentialBytes);
        
        var parts = credentials.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var username = parts[0];
        var password = parts[1];
        
        // Compare with configured credentials (RouterConfig.AuthUsername/Password)
        return username == _config.AuthUsername && password == _config.AuthPassword;
    }
    catch
    {
        return false;
    }
}
```

## Configuration

**Source:** `RouterConfig` records:

```csharp
public sealed record RouterConfig
{
    public string AuthUsername { get; init; } = "admin";
    public string AuthPassword { get; init; } = ""; // Prefer env var LLAMODEM_AUTH_PASSWORD
    
    // ... other config
}
```

**Environment variable override:**
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

**Implementation:** Path prefix check in middleware (`path.StartsWith("/v1")`).

## Error Response

**Status code:** 401 Unauthorized

**WWW-Authenticate header:** `Basic realm="LlaModem"`

**Body:**
```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Invalid credentials"
  }
}
```

**Writer:** `ErrorResponseWriter.WriteAsync()` (structured error format).

## Usage in Program.cs

**Extension method registration:**
```csharp
app.UseBasicAuthWhen("/v1"); // Extension method on WebApplication
```

**Extension method implementation:**
```csharp
public static class BasicAuthExtensions
{
    public static IApplicationBuilder UseBasicAuthWhen(
        this WebApplication app, 
        string pathPrefix)
    {
        var routerConfig = app.Services.GetRequiredService<IOptions<RouterConfig>>().Value;
        
        return app.UseWhen(
            context => context.Request.Path.StartsWithSegments(pathPrefix),
            appBuilder =>
            {
                appBuilder.UseMiddleware<BasicAuthMiddleware>(
                    routerConfig.AuthUsername,
                    routerConfig.AuthPassword);
            });
    }
}
```

## Security Notes

- **Basic Auth only:** No Bearer tokens, OAuth, or API keys supported
- **Plaintext credentials:** Base64 encoding — not encryption (requires HTTPS)
- **Local network exposure:** Admin endpoints unauthenticated — intended for trusted environments only
- **Password storage:** Plain text in config / environment variables (not hashed)

## Testing Patterns

- **HttpContext mock:** Test auth header parsing and validation
- **Route filtering:** Confirm `/admin/*` routes bypass auth
- **Invalid credentials:** Verify 401 response with correct headers
- **Missing auth header:** Treat as unauthorized (no anonymous access)

See `.rpiv/guidance/LlaModem.Tests/Middleware/BasicAuthMiddlewareTests.cs`.

## Workflow: Changing Auth Credentials

1. Update `appsettings.json`:
   ```json
   "Router": {
     "AuthUsername": "new-username",
     "AuthPassword": "new-password"
   }
   ```

2. Or set environment variables (preferred for deployment):
   ```bash
   export LLAMODEM_AUTH_USERNAME="new-username"
   export LLAMODEM_AUTH_PASSWORD="new-password"
   dotnet run
   ```

3. Test with curl:
   ```bash
   # Valid credentials
   curl -u new-username:new-password http://localhost:9000/v1/chat/completions
   
   # Invalid credentials (401)
   curl -u wrong:wrong http://localhost:9000/v1/chat/completions
   ```

## Critical Notes

- **Admin endpoints open:** `/admin/*` routes bypass auth — security risk on public networks
- **No HTTPS:** Basic Auth credentials sent in plaintext (use reverse proxy with TLS)
- **Single user:** No role-based access control — all authenticated users have full access
- **Password rotation:** Requires app restart (config loaded at startup)
