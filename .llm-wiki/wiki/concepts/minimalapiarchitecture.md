# MinimalAPIArchitecture

**Concept Type:** Architecture Pattern  
**Description:** ASP.NET Core Minimal API pattern with extension methods for endpoint registration, avoiding controller boilerplate.

## Overview

LlaModem uses ASP.NET Core Minimal API (introduced in .NET 6) instead of traditional MVC controllers. This provides:
- Concise endpoint definitions
- No controller classes or attributes needed
- Inline handler functions or service injection
- Extension method pattern for organization

## Endpoint Registration Pattern

### Extension Method Approach

```csharp
// Program.cs
app.MapStatsEndpoints(); // Extension method on WebApplication

// StatsEndpointExtensions.cs
public static class StatsEndpointExtensions
{
    public static WebApplication MapStatsEndpoints(this WebApplication app)
    {
        var dashboard = app.Services.GetRequiredService<DashboardService>();
        
        app.MapGet("/health", async () => 
        {
            var activeModel = await dashboard.GetActiveModelNameAsync();
            return Results.Ok(new { healthy = true, activeModel });
        });
        
        app.MapPost("/admin/model", (HttpContext ctx, ModelSwitchRequest req) => 
        {
            // Handler logic
        });
        
        return app;
    }
}
```

**Benefits:**
- Organized by feature/domain
- Reusable across projects
- Clean separation of concerns
- Easy to test independently

### Inline Handlers

```csharp
// Direct registration (less common)
app.MapPost("/admin/stop", async () => 
{
    var modelManager = app.Services.GetRequiredService<IMetaModelManager>();
    await modelManager.StopActiveModelAsync();
    return Results.Ok();
});
```

## Service Injection Pattern

Services injected via `app.Services.GetRequiredService<T>()`:

```csharp
app.MapGet("/admin/status", async (HttpContext ctx) => 
{
    var modelManager = ctx.RequestServices.GetRequiredService<IMetaModelManager>();
    var status = await modelManager.GetActiveModelStateAsync();
    return Results.Ok(status);
});
```

**Alternative:** Inject services into handler closure (as shown above).

## Response Formatting

### Success Responses

```csharp
return Results.Ok(new { healthy = true, activeModel = "qwen36-smart" });
```

### Error Responses

Structured errors via `ErrorResponseWriter.WriteAsync()`:
```csharp
await ErrorResponseWriter.WriteAsync(
    context, 
    "BACKEND_UNAVAILABLE", 
    "llama-server backend not responding", 
    StatusCodes.Status503);
```

Produces consistent JSON:
```json
{ "error": { "code": "BACKEND_UNAVAILABLE", "message": "..." } }
```

## Middleware Integration

Endpoints work seamlessly with middleware pipeline:

1. `ResponseUsageMiddleware` captures response body
2. `RequestLoggingMiddleware` logs request details
3. `BasicAuthMiddleware` validates credentials (for `/v1/*`)
4. Endpoint handlers execute

**Note:** Middleware runs before endpoints, allowing cross-cutting concerns (auth, logging, usage capture).

## Testing Pattern

Endpoints not tested directly (routing covered by integration tests). Instead:
- Test service logic via unit tests
- Mock dependencies (`IModelRepository`, fake persistence)
- Verify return values and side effects

See [[concepts/LlaModem.Tests]] for test patterns.

## Related Entities

- [[entities/DashboardService]] - Admin endpoint handlers (status, stats, model control)
- [[entities/RequestForwarder]] - Proxy to llama-server backends (`/v1/*` routes)
- [[entities/BasicAuthMiddleware]] - Validates credentials on `/v1/*` routes

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together