# LlaModem Architecture Overview

## Synthesis

This document synthesizes the architecture of LlaModem, a lightweight router for local LLM servers that exposes an OpenAI-compatible API. The system routes requests to one of several `llama-server` backends based on the `X-Llama-Model` header, with automatic model lifecycle management including lazy start, hot switching, idle shutdown, and health checks.

## Core Architecture Principles

### 1. Layered Architecture with DI

The project follows a layered architecture with dependency injection:
- **Configuration layer** (`Config/`) - Immutable POCO records bound via `IOptions<T>`
- **Service layer** (`Services/`) - Business logic and infrastructure services
- **Middleware layer** (`Middleware/`) - Cross-cutting concerns (auth, logging, usage capture)
- **Endpoint layer** - Minimal API endpoints organized via extension methods

### 2. Interface-First Service Design

All services implement interfaces for testability:
- `IModelManager` / `IMetaModelManager` - Model lifecycle orchestration
- `IRequestForwarder` - HTTP proxy logic
- `IUsageService` - Usage tracking and persistence
- `IDashboardService` - Admin endpoint handlers

### 3. Minimal API Pattern

Endpoints defined via extension methods rather than controllers:
```csharp
app.MapStatsEndpoints(); // Extension method on WebApplication
```

This provides concise endpoint definitions with no controller boilerplate.

## Key Components

### Configuration Records

Immutable configuration records in `Config/`:
- **AppConfig** - Root config containing all sections
- **RouterConfig** - Router settings (URL, auth, timeouts, header injection)
- **ModelConfig** - Per-model configuration (start script, backend URL)
- **UsageConfig** - Usage tracking settings (enabled flag, database path)

All records are POCO with no behavior, bound via `IOptions<T>` pattern.

### Model Lifecycle Management

The `ModelManager` orchestrates model lifecycle:
1. **Lazy start** - Models only launch when first requested
2. **Hot switching** - Request a different model → current stops, new starts
3. **Idle shutdown** - Stops active model after 600s of inactivity (default)
4. **Health checks** - Poll llama-server `/health` endpoint until healthy or timeout
5. **Process cleanup** - On app shutdown, all tracked PowerShell processes terminated

### Usage Tracking Pattern

Token usage statistics captured from non-streaming responses:
1. `ResponseUsageMiddleware` buffers response body (non-streaming only)
2. `UsageExtractor.Extract()` parses JSON for token counts and timing data
3. `SessionEntry` created with metadata (model, route, client IP, headers)
4. `SqliteUsagePersistence.Insert()` writes to SQLite via Dapper
5. `ModelMetricsService.Update()` updates in-memory aggregation

### Header Injection Pattern

HTTP request headers mapped to JSON body fields at root level:
- Configuration: `RouterConfig.BodyHeaderMappings` (e.g., `"x-client-id": "clientId"`)
- Auto-typed values: boolean, integer, double, or string based on content
- Case-insensitive matching for header names

## Data Flow

### Request Processing Pipeline

1. **ResponseUsageMiddleware** - Captures response body for usage tracking
2. **RequestLoggingMiddleware** - Logs request details (debug level)
3. **BasicAuthMiddleware** - Validates credentials on `/v1/*` routes
4. **Endpoint handlers**:
   - Proxy routes (`/v1/*`) → `RequestForwarder` forwards to backend
   - Admin routes (`/admin/*`) → `DashboardService` handles status/stats

### Model Lifecycle Flow

```
Client Request (X-Llama-Model: qwen36-smart)
  ↓
ModelManager.EnsureModelAsync()
  ↓
DefaultModelLauncher.StartAsync()
  ↓
PowerShell script launches llama-server
  ↓
HealthChecker.PollAsync() → /health endpoint
  ↓
StateRepository.SetStateAsync() → process tracking
  ↓
RequestForwarder.ForwardAsync() → backend proxy
```

## External Integrations

| Dependency | Purpose |
|------------|---------|
| `llama-server` | LLM backend (runs GGUF models via CLI) |
| SQLite / Dapper | Usage statistics persistence |
| Serilog | Structured logging (console + file sinks) |
| Scalar.AspNetCore | OpenAPI UI / API reference |
| System.Management | GPU memory inspection via nvidia-smi |

## Testing Strategy

- **xUnit** test project (`LlaModem.Tests`)
- **In-memory mocks**: `InMemoryModelRepository`, fake HTTP clients
- **Focus areas**: service logic, middleware behavior, utility functions
- **Not tested**: endpoint routing (covered by integration tests in Minimal API pattern)

## Related Documentation

### Entities
- [[entities/DashboardService]] - Admin endpoints for model control and stats
- [[entities/UsageService]] - SQLite usage tracking persistence
- [[entities/RequestForwarder]] - HTTP proxy to llama-server backends
- [[entities/ResponseUsageMiddleware]] - Usage capture middleware
- [[entities/RequestLoggingMiddleware]] - Debug request logging
- [[entities/BasicAuthMiddleware]] - Basic Auth on proxy routes
- [[entities/DefaultModelLauncher]] - PowerShell process management
- [[entities/HeaderValueInjector]] - Header-to-body mapping service
- [[entities/UsageExtractor]] - Token usage parsing utility
- [[entities/ConfigRecords]] - Immutable configuration records
- [[entities/ModelManager]] - Lifecycle orchestration service
- [[entities/StatsService]] - Usage aggregation and cost comparison

### Concepts
- [[concepts/ModelLifecycleManagement]] - Automatic model lifecycle pattern
- [[concepts/UsageTrackingPattern]] - SQLite persistence for token stats
- [[concepts/HeaderInjectionPattern]] - HTTP header to JSON body mapping
- [[concepts/MinimalAPIArchitecture]] - Minimal API endpoint patterns

## Critical Notes

- **Sequential execution**: All models share port 8001 (not concurrent)
- **No streaming support**: Full response buffered for usage capture
- **Admin endpoints open**: `/admin/*` routes unauthenticated (trusted environment only)
- **PowerShell scripts**: Model launchers use `%VAR%` syntax for env var expansion at startup