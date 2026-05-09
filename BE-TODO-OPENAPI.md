# Backend OpenAPI Schema Implementation - TODO

## Goal
Add proper OpenAPI response/request schemas to backend endpoints so the frontend can generate clean TypeScript types via `@hey-api/openapi-ts`.

---

## Current Status

### ✅ What Works
- OpenAPI JSON available at `/openapi/v1.json`
- Scalar UI at `/scalar`
- Frontend configured with `@hey-api/vite-plugin`
- Manual type wrappers in `src/client/operations.ts`

### ❌ What's Missing
- Response schemas are `unknown` (all endpoints)
- Request body schemas missing (POST/PUT endpoints)
- Query parameter definitions missing
- Path parameter definitions missing
- Error response schemas missing

---

## Priority: Implement in This Order

### Phase 1: Critical Dashboard Endpoints (High Priority)
**Target:** `/admin/dashboard/*` endpoints

#### 1.1 GET `/admin/dashboard/models`
```csharp
// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "List of all models",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "array",
                Items = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["name"] = new OpenApiSchema { Type = "string" },
                        ["status"] = new OpenApiSchema { Type = "string" },
                        ["currentTokensPerSecond"] = new OpenApiSchema { Type = "number", Format = "double" },
                        ["averageTokensPerSession"] = new OpenApiSchema { Type = "number", Format = "double", Nullable = true },
                        ["parameters"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["temperature"] = new OpenApiSchema { Type = "number", Format = "double" }, ["topP"] = new OpenApiSchema { Type = "number", Format = "double" }, ["topK"] = new OpenApiSchema { Type = "number", Format = "int32" }, ["minP"] = new OpenApiSchema { Type = "number", Format = "double" }, ["presencePenalty"] = new OpenApiSchema { Type = "number", Format = "double" }, ["repetitionPenalty"] = new OpenApiSchema { Type = "number", Format = "double" } } },
                        ["scriptPath"] = new OpenApiSchema { Type = "string" },
                        ["processId"] = new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
                        ["startedAt"] = new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
                        ["lastRequestAt"] = new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
                        ["isHealthy"] = new OpenApiSchema { Type = "boolean", Nullable = true },
                        ["lastError"] = new OpenApiSchema { Type = "string", Nullable = true }
                    }
                }
            }
        }
    }
};
```

#### 1.2 GET `/admin/dashboard/models/{name}`
```csharp
// Add path parameter
operation.Parameters.Add(new OpenApiParameter
{
    Name = "name",
    In = ParameterLocation.Path,
    Required = true,
    Schema = new OpenApiSchema { Type = "string", Description = "Model name" }
});

// Add response schema (same as 1.1 but for single object)
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Model details",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema { /* Same schema as 1.1 Items */ }
        }
    }
};

operation.Responses["404"] = new OpenApiResponse { Description = "Model not found" };
```

#### 1.3 POST `/admin/dashboard/models/{name}/start`
```csharp
// Add path parameter
operation.Parameters.Add(new OpenApiParameter
{
    Name = "name",
    In = ParameterLocation.Path,
    Required = true,
    Schema = new OpenApiSchema { Type = "string", Description = "Model name" }
});

// Add request body
operation.RequestBody = new OpenApiRequestBody
{
    Required = true,
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["temperature"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Temperature (0.0-1.0)" },
                    ["topP"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Top P (0.0-1.0)" },
                    ["topK"] = new OpenApiSchema { Type = "integer", Format = "int32", Description = "Top K" },
                    ["minP"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Min P (0.0-1.0)" },
                    ["presencePenalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Presence penalty" },
                    ["repetitionPenalty"] = new OpenApiSchema { Type = "number", Format = "double", Description = "Repetition penalty" }
                }
            }
        }
    }
};

// Add response schemas
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Model started successfully",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["message"] = new OpenApiSchema { Type = "string" },
                    ["processId"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                    ["startedAt"] = new OpenApiSchema { Type = "string", Format = "date-time" }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "Invalid request" };
operation.Responses["404"] = new OpenApiResponse { Description = "Model not found" };
operation.Responses["503"] = new OpenApiResponse { Description = "Failed to start model" };
```

#### 1.4 POST `/admin/dashboard/models/{name}/stop`
```csharp
// Add path parameter
operation.Parameters.Add(new OpenApiParameter
{
    Name = "name",
    In = ParameterLocation.Path,
    Required = true,
    Schema = new OpenApiSchema { Type = "string", Description = "Model name" }
});

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Model stopped successfully",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["message"] = new OpenApiSchema { Type = "string" }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "Model not active" };
operation.Responses["404"] = new OpenApiResponse { Description = "Model not found" };
operation.Responses["503"] = new OpenApiResponse { Description = "Failed to stop model" };
```

#### 1.5 PUT `/admin/dashboard/models/{name}/params`
```csharp
// Add path parameter
operation.Parameters.Add(new OpenApiParameter
{
    Name = "name",
    In = ParameterLocation.Path,
    Required = true,
    Schema = new OpenApiSchema { Type = "string", Description = "Model name" }
});

// Add request body (same as 1.3)
operation.RequestBody = new OpenApiRequestBody
{
    Required = true,
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["temperature"] = new OpenApiSchema { Type = "number", Format = "double" },
                    ["topP"] = new OpenApiSchema { Type = "number", Format = "double" },
                    ["topK"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                    ["minP"] = new OpenApiSchema { Type = "number", Format = "double" },
                    ["presencePenalty"] = new OpenApiSchema { Type = "number", Format = "double" },
                    ["repetitionPenalty"] = new OpenApiSchema { Type = "number", Format = "double" }
                }
            }
        }
    }
};

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Parameters updated successfully",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["message"] = new OpenApiSchema { Type = "string" },
                    ["restartRequired"] = new OpenApiSchema { Type = "boolean" },
                    ["updatedParams"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["temperature"] = new OpenApiSchema { Type = "number", Format = "double" }, ["topP"] = new OpenApiSchema { Type = "number", Format = "double" }, ["topK"] = new OpenApiSchema { Type = "number", Format = "int32" }, ["minP"] = new OpenApiSchema { Type = "number", Format = "double" }, ["presencePenalty"] = new OpenApiSchema { Type = "number", Format = "double" }, ["repetitionPenalty"] = new OpenApiSchema { Type = "number", Format = "double" } } }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "Invalid request" };
operation.Responses["404"] = new OpenApiResponse { Description = "Model not found" };
```

#### 1.6 GET `/admin/dashboard/models/{name}/health`
```csharp
// Add path parameter
operation.Parameters.Add(new OpenApiParameter
{
    Name = "name",
    In = ParameterLocation.Path,
    Required = true,
    Schema = new OpenApiSchema { Type = "string", Description = "Model name" }
});

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Health check successful",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["isHealthy"] = new OpenApiSchema { Type = "boolean" },
                    ["healthUrl"] = new OpenApiSchema { Type = "string" },
                    ["lastCheckedAt"] = new OpenApiSchema { Type = "string", Format = "date-time" },
                    ["responseTimeMs"] = new OpenApiSchema { Type = "number", Format = "double", Nullable = true }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "Model not running" };
```

---

### Phase 2: Stats Endpoints (Medium Priority)
**Target:** `/admin/stats/*` endpoints

#### 2.1 GET `/admin/stats/usage`
```csharp
// Add query parameters
operation.Parameters.Add(new OpenApiParameter { Name = "days", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Format = "int32", Default = new OpenApiInteger(30), Minimum = new OpenApiInteger(1), Maximum = new OpenApiInteger(365), Description = "Number of days (default: 30, max: 365)" } });
operation.Parameters.Add(new OpenApiParameter { Name = "model", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Description = "Optional model name to filter usage data" } });

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Daily usage statistics",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["period"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["from"] = new OpenApiSchema { Type = "string", Format = "date" }, ["to"] = new OpenApiSchema { Type = "string", Format = "date" } } },
                    ["summary"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["totalRequests"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["totalPromptTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["totalCompletionTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["totalTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["avgPromptMs"] = new OpenApiSchema { Type = "number", Format = "double" }, ["avgCompletionMs"] = new OpenApiSchema { Type = "number", Format = "double" }, ["cacheHitRate"] = new OpenApiSchema { Type = "number", Format = "double" } } },
                    ["daily"] = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["date"] = new OpenApiSchema { Type = "string", Format = "date" }, ["model"] = new OpenApiSchema { Type = "string" }, ["requests"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["promptTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["completionTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["totalTokens"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["avgPromptMs"] = new OpenApiSchema { Type = "number", Format = "double" }, ["avgCompletionMs"] = new OpenApiSchema { Type = "number", Format = "double" }, ["cacheHitRate"] = new OpenApiSchema { Type = "number", Format = "double" } } } }
                }
            }
        }
    }
};
```

#### 2.2 GET `/admin/stats/requests`
```csharp
// Add query parameters
operation.Parameters.Add(new OpenApiParameter { Name = "limit", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Format = "int32", Default = new OpenApiInteger(50), Minimum = new OpenApiInteger(1), Maximum = new OpenApiInteger(500), Description = "Number of requests (default: 50, max: 500)" } });
operation.Parameters.Add(new OpenApiParameter { Name = "offset", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Format = "int32", Default = new OpenApiInteger(0), Minimum = new OpenApiInteger(0), Description = "Number of requests to skip (default: 0)" } });
operation.Parameters.Add(new OpenApiParameter { Name = "model", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Description = "Optional model name to filter requests" } });

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Recent requests",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["total"] = new OpenApiSchema { Type = "integer", Format = "int64" },
                    ["offset"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                    ["limit"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                    ["items"] = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["id"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ["timestamp"] = new Open
#### 2.3 GET `/admin/stats/cost-comparison`
```csharp
// Add query parameters
operation.Parameters.Add(new OpenApiParameter { Name = "days", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Format = "int32", Default = new OpenApiInteger(30), Minimum = new OpenApiInteger(1), Maximum = new OpenApiInteger(365), Description = "Number of days to analyze (default: 30, max: 365)" } });
operation.Parameters.Add(new OpenApiParameter { Name = "model", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Description = "Optional model name to filter usage data" } });

// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Cost comparison",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["period"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["from"] = new OpenApiSchema { Type = "string", Format = "date" }, ["to"] = new OpenApiSchema { Type = "string", Format = "date" } } },
                    ["model"] = new OpenApiSchema { Type = "string", Nullable = true },
                    ["models"] = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["name"] = new OpenApiSchema { Type = "string" }, ["promptCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["completionCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["totalCost"] = new OpenApiSchema { Type = "number", Format = "double" } } } },
                    ["cheapest"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["name"] = new OpenApiSchema { Type = "string" }, ["promptCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["completionCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["totalCost"] = new OpenApiSchema { Type = "number", Format = "double" } } },
                    ["mostExpensive"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["name"] = new OpenApiSchema { Type = "string" }, ["promptCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["completionCost"] = new OpenApiSchema { Type = "number", Format = "double" }, ["totalCost"] = new OpenApiSchema { Type = "number", Format = "double" } } }
                }
            }
        }
    }
};
```

---

### Phase 3: Admin Endpoints (Low Priority)
**Target:** `/admin/*` endpoints

#### 3.1 GET `/admin/status`
```csharp
// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Backend status",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["activeModel"] = new OpenApiSchema { Type = "string", Nullable = true },
                    ["backendUrl"] = new OpenApiSchema { Type = "string" }
                }
            }
        }
    }
};
```

#### 3.2 POST `/admin/model`
```csharp
// Add request body
operation.RequestBody = new OpenApiRequestBody
{
    Required = true,
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["model"] = new OpenApiSchema { Type = "string", Description = "Model name to start" }
                }
            }
        }
    }
};

// Add response schemas
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Model started successfully",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["activeModel"] = new OpenApiSchema { Type = "string" }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "Bad request - invalid model name" };
operation.Responses["503"] = new OpenApiResponse { Description = "Service unavailable - failed to start model" };
```

#### 3.3 POST `/admin/stop`
```csharp
// Add response schema
operation.Responses["200"] = new OpenApiResponse
{
    Description = "Model stopped successfully",
    Content = new Dictionary<string, OpenApiMediaType>
    {
        ["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["message"] = new OpenApiSchema { Type = "string" }
                }
            }
        }
    }
};

operation.Responses["400"] = new OpenApiResponse { Description = "No active model to stop" };
```

---

## Implementation Methods

### Method A: Use `[ProducesResponseType]` Attributes (Quickest)

Add ASP.NET Core attributes directly to endpoint methods:

```csharp
[ProducesResponseType(typeof(DailyUsageResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(OpenApiEmptyResponse), StatusCodes.Status400BadRequest)]
[HttpGet("/admin/stats/usage")]
public async Task<DailyUsageResponse> GetDailyUsage(int days = 30, string? model = null)
{
    // ... implementation
}
```

**Pros:**
- Minimal code changes
- Types auto-generated from C# classes
- No manual schema definitions

**Cons:**
- Requires Swashbuckle or NSwag
- Limited control over schema details

---

### Method B: Manual OpenApiDocumentTransformer (Most Flexible)

Create a custom transformer to apply schemas programmatically:

```csharp
// OpenApiDocumentTransformer.cs
public class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public OpenApiDocument Transform(OpenApiDocument document, OpenApiDocumentTransformerContext context)
    {
        // Read endpoint metadata and apply schemas based on endpoint names
        // This requires reading the endpoint metadata manually
        
        foreach (var path in document.Paths)
        {
            switch (path.Key)
            {
                case "/admin/stats/usage":
                    // Apply schema manually
                    break;
                // ... more cases
            }
        }
        
        return document;
    }
}
```

Register in Program.cs:
```csharp
builder.Services.AddOpenApiDocument(config =>
{
    config.AddDocumentTransformer<OpenApiDocumentTransformer>();
});
```

**Pros:**
- Full control over all OpenAPI details
- No dependency on Swashbuckle/NSwag

**Cons:**
- More complex implementation
- Manual schema definitions required

---

### Method C: Use Swashbuckle with Source Generation (Recommended)

Configure Swashbuckle to automatically generate schemas from C# types:

```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations(); // Enable [ProducesResponseType] support
    c.UseAllOfToExtendReferenceSchemas();
    c.UseInlineDefinitionsForEnums();
});
```

**Pros:**
- Automatic schema generation
- Types match C# models
- Well-maintained library

**Cons:**
- Requires Swashbuckle dependency
- May need additional configuration

---

## Quick Start: Minimal Implementation

### Step 1: Create OpenAPI Schema Helper Class

```csharp
// OpenApiSchemas.cs
public static class OpenApiSchemas
{
    public static OpenApiSchema ModelDashboardItemSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["name"] = new OpenApiSchema { Type = "string" },
            ["status"] = new OpenApiSchema { Type = "string" },
            ["currentTokensPerSecond"] = new OpenApiSchema { Type = "number", Format = "double" },
            // ... all other properties
        }
    };
    
    public static OpenApiSchema DailyUsageResponseSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["period"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["from"] = new OpenApiSchema { Type = "string", Format = "date" }, ["to"] = new OpenApiSchema { Type = "string", Format = "date" } } },
            ["summary"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["totalRequests"] = new OpenApiSchema { Type = "integer", Format = "int64" }, ... } },
            ["daily"] = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["date"] = new OpenApiSchema { Type = "string", Format = "date" }, ... } } }
        }
    };
    
    // ... more schemas
}
```

### Step 2: Create Endpoint Metadata Extension

```csharp
// EndpointMetadataExtensions.cs
public static class EndpointMetadataExtensions
{
    public static IEndpointConventionBuilder WithOpenApiResponses(this IEndpointConventionBuilder builder)
    {
        builder.MapStatsEndpointsWithOpenApi();
        builder.MapAdminEndpointsWithOpenApi();
        builder.MapDashboardEndpointsWithOpenApi();
        return builder;
    }
}
```

### Step 3: Apply to Endpoints

```csharp
// In Program.cs, replace existing endpoint configurations with:
var statsGroup = endpoints.MapGroup("/admin/stats");
statsGroup.MapGet("/usage", async (IStatsService stats, int days = 30, string? model = null) =>
{
    days = Math.Clamp(days, 1, 365);
    var response = await stats.GetDailyUsageAsync(days, model);
    return Results.Json(response);
})
.WithOpenApi(operation =>
{
    operation.Summary = "Get daily usage statistics";
    operation.Parameters.Add(new OpenApiParameter { Name = "days", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Format = "int32", Default = new OpenApiInteger(30), Minimum = new OpenApiInteger(1), Maximum = new OpenApiInteger(365) } });
    operation.Parameters.Add(new OpenApiParameter { Name = "model", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
    operation.Responses["200"] = new OpenApiResponse
    {
        Description = "Daily usage statistics",
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new OpenApiMediaType { Schema = OpenApiSchemas.DailyUsageResponseSchema }
        }
    };
    return operation;
});
```

---

## Validation Checklist

After implementing any endpoint:
- [ ] `curl http://localhost:9000/openapi/v1.json` returns valid JSON
- [ ] Response schema is not `unknown`
- [ ] Query parameters are defined (if applicable)
- [ ] Request body is defined (if applicable)
- [ ] Error responses are defined (400, 404, 503)
- [ ] Frontend `npm run build` generates proper TypeScript types
- [ ] Frontend TypeScript compiles without errors

---

## Testing the Frontend

Once backend schemas are added:

1. **Start backend:**
   ```bash
   cd ~/RiderProjects/llamodem
   dotnet run
   ```

2. **Verify OpenAPI JSON:**
   ```bash
   curl http://localhost:9000/openapi/v1.json | jq '.paths'
   ```

3. **Build frontend:**
   ```bash
   cd ~/RiderProjects/llamodem-FE/fe-stats
   npm run build
   ```

4. **Check generated types:**
   ```bash
   cat src/client/types.gen.ts | head -50
   ```

5. **Remove manual type wrappers:**
   - Delete `src/client/operations.ts`
   - Update hooks to use generated types directly
   - Update `src/client/index.ts` to export generated operations

---

## Rollback Plan

If issues arise:
```bash
cd ~/RiderProjects/llamodem-FE/fe-stats
# Remove generated types
rm -rf src/client/

# Restore old API clients
git checkout HEAD -- src/api/

# Revert config changes
git checkout HEAD -- vite.config.ts
rm openapi-ts.config.ts
rm .env-local
```

---

## Resources

- [OpenAPI Specification](https://spec.openapis.org/oas/v3.1.0)
- [@hey-api/openapi-ts](https://heyapi.dev/openapi-ts)
- [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [Microsoft.AspNetCore.OpenApi](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/)

---

## Next Steps

1. **Choose implementation method** (Method A/B/C)
2. **Implement Phase 1** (Dashboard endpoints - 6 endpoints)
3. **Test frontend build**
4. **Implement Phase 2** (Stats endpoints - 3 endpoints)
5. **Implement Phase 3** (Admin endpoints - 3 endpoints)
6. **Remove manual type wrappers**
7. **Update hooks to use generated types directly**
8. **Test all frontend pages**
