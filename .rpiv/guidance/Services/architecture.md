# Services Layer

## Responsibility

Core business logic and infrastructure services for model lifecycle management, request forwarding, usage tracking, and admin operations. All services are registered as singletons in DI container.

## Module Structure

```
Services/
├── [Interfaces]          — I* interfaces (20 files)
├── AdminRouteFilter.cs   — OpenAPI document transformer (hides /admin/* from docs)
├── ApiResult.cs         — Generic result type for async operations
├── DashboardService.cs  — Admin endpoint handlers (status, stats, model control)
├── DbSchema.cs          — SQLite schema + Dapper queries for usage persistence
├── DefaultModelLauncher.cs — PowerShell process launcher with health polling
├── ErrorResponseWriter.cs — Structured error responses
├── GpuMemoryChecker.cs  — VRAM inspection via nvidia-smi (deprecated)
├── HeaderValueInjector.cs — Maps HTTP headers to JSON body fields
├── HealthChecker.cs     — Polls llama-server /health endpoint
├── HttpConstants.cs     — Route prefixes, header names, content types
├── IdleTimeoutService.cs — Background host service for idle model shutdown
├── InMemoryModelRepository.cs — In-memory process state tracking
├── LaunchParamParser.cs — Parses launch parameters from headers
├── ModelLaunchParams.cs — Temperature, top_p, presence_penalty values
├── ModelManager.cs      — Core lifecycle: lazy start, hot switch, idle shutdown
├── ModelMetricsService.cs — Token usage metrics aggregation
├── ModelPricing.cs      — Cloud model pricing for cost comparison
├── ModelProxyHandler.cs — Prepares and sends requests to llama-server
├── OpenApiDocumentTransformer.cs — Adds Scalar/OpenAPI extensions
├── ProcessKiller.cs     — Graceful process termination with timeout fallback
├── RequestForwarder.cs  — Proxies client requests to backend
├── SqliteUsagePersistence.cs — Dapper-based SQLite persistence layer
├── StatsService.cs      — Aggregates usage data for admin endpoints
├── SystemIdleTracker.cs — Tracks request idle time
├── UsageExtractor.cs    — Parses token usage from llama-server JSON responses
└── UsageService.cs      — Records session entries to metrics service
```

## Interface Pattern

All services follow interface-first pattern:
- Interfaces named `I*` (e.g., `IModelRepository`, `IRequestForwarder`)
- Concrete implementations have no `I` prefix (e.g., `InMemoryModelRepository`, `RequestForwarder`)
- Registered in `Program.cs`: `builder.Services.AddSingleton<I*, Concrete>()`

## Key Architectural Patterns

### Model Lifecycle State Management

**State repository pattern:**
```csharp
public interface IModelRepository
{
    Task<ModelProcessState?> GetStateAsync(string modelName, CancellationToken ct);
    Task SetStateAsync(ModelProcessState state, CancellationToken ct);
    Task ClearStateAsync(CancellationToken ct);
    Task<IReadOnlyList<ModelProcessState>> GetAllStatesAsync(CancellationToken ct);
}

public record ModelProcessState(
    string ModelName,
    int ProcessId,
    DateTimeOffset StartedAt);
```

**Implementation:** `InMemoryModelRepository` — in-memory dictionary keyed by model name.

### Request Forwarding Pipeline

```
Client → HeaderValueInjector (inject headers to body) 
       → ModelProxyHandler (select backend URL, prepare request)
       → RequestForwarder (proxy to llama-server)
       → ResponseUsageMiddleware (capture usage stats)
```

**Header injection:** HTTP headers mapped to JSON root fields via `RouterConfig.BodyHeaderMappings`.

### Usage Capture Flow

```
Non-streaming response 
  → buffer body in memory 
  → parse JSON for token usage 
  → create SessionEntry 
  → record to UsageService 
  → persist to SQLite via Dapper
```

**Streaming responses:** Skipped (usage not available until completion).

## Service Responsibilities

### ModelManager (IMetaModelManager)

- **Lazy start**: Launch model on first request if not running
- **Hot switching**: Stop current model, start requested model
- **Health checks**: Poll `/health` endpoint with configurable timeout/delay
- **Idle shutdown**: Delegates to `IdleTimeoutService` for background tracking
- **Process cleanup**: Triggers on app shutdown via `ShutdownAsync()`

**Dependencies:** `IModelRepository`, `DefaultModelLauncher`, `HealthChecker`, `GpuMemoryChecker`, `ProcessKiller`

### RequestForwarder (IRequestForwarder)

- Proxies HTTP requests to llama-server backend
- Preserves method, path, headers, body
- Handles errors via `ErrorResponseWriter`
- No streaming support (buffers full response for usage capture)

**Dependencies:** `IModelProxyHandler`, `ILogger`

### DashboardService

Admin endpoint handlers:
- `/health` — Router health + active model name
- `/admin/status` — Current model state and backend URL
- `/admin/model` — Switch to specified model
- `/admin/stop` — Stop active model
- `/admin/stats/*` — Usage statistics endpoints

**Dependencies:** `IMetaModelManager`, `IStatsService`, `IUsageService`

### StatsService (IStatsService)

Aggregates usage data from `SessionEntry`:
- Daily token usage summaries (`?days=30&model=name`)
- Paginated recent requests (`?limit=50&offset=0`)
- Cost comparison estimates (cloud model pricing)

**Dependencies:** `SqliteUsagePersistence`, `ModelPricing`

### UsageService (IUsageService)

Records session entries to metrics service:
- Parses token usage from JSON responses
- Creates `SessionEntry` with timing data
- Updates `ModelMetricsService` for real-time metrics

**Dependencies:** `IUsagePersistence`, `ModelMetricsService`, `ILogger`

## Background Services

### IdleTimeoutService (IHostedService)

Background service that:
- Tracks idle time via `SystemIdleTracker`
- Triggers model shutdown after `IdleTimeoutSeconds` of inactivity
- Resets on each incoming request (via `IIdleTimeoutResetter`)

**Registration:** `builder.Services.AddHostedService<IdleTimeoutService>()`

### ModelMetricsService

In-memory metrics aggregation:
- Tracks token usage over time
- Computes cost estimates based on `ModelPricing`
- Used by `StatsService` for admin endpoints

## Error Handling

**Structured errors:** `ErrorResponseWriter.WriteAsync()` produces consistent error JSON:
```json
{ "error": { "code": "MODEL_NOT_FOUND", "message": "..." } }
```

**Error codes:** Defined in `ApiResult<T>` generic result type.

## Testing Patterns

- **InMemoryModelRepository**: Replaces state repository for unit tests
- **Fake HTTP clients**: Mock `IHttpClientFactory` responses
- **Dependency injection**: Services injected via constructor with fake implementations

See `.rpiv/guidance/LlaModem.Tests/architecture.md` for test structure.

## Cross-Boundary Patterns

### ModelManager ↔ DefaultModelLauncher

**Boundary:** Lifecycle management → process execution
```csharp
// ModelManager calls:
var process = await _launcher.StartAsync(modelName, startScript, launchParams);

// Launcher returns:
Process process (stdout/stderr captured via events)
```

### UsageExtractor → SessionEntry

**Boundary:** Raw JSON → structured usage data
```csharp
// Extractor parses llama-server response:
var usage = UsageExtractor.Extract(rawJson); // TokenUsage?

// Creates session entry:
var entry = new SessionEntry(
    Timestamp: DateTimeOffset.UtcNow,
    Model: model,
    Route: route,
    Usage: usage with { RequestTime, ResponseTime },
    ClientIp: clientIp,
    StatusCode: statusCode,
    RequestHeaders: headers);
```

## Add New Service Checklist

1. Create interface `INewService` in `Services/`
2. Create implementation `NewService` in `Services/`
3. Register in `Program.cs`: `builder.Services.AddSingleton<INewService, NewService>()`
4. Inject via constructor in consuming classes
5. Add tests in `LlaModem.Tests/` with in-memory mocks
