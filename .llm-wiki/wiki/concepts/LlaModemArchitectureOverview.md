---
type: concept
created: 2026-05-19
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-004]]
status: complete
---

# LlaModemArchitectureOverview

**Description:** High-level architecture overview of LlaModem — a lightweight router for local LLM servers exposing an OpenAI-compatible API with automatic model lifecycle management.

## Architecture Pattern

Layered architecture with dependency injection:
- **No strict Clean Architecture** — single project structure
- **Service-layer organization** — interfaces + concrete implementations in `Services/`
- **Configuration records** — immutable POCO records in `Config/`
- **Minimal API pattern** — ASP.NET Core Minimal API with extension methods (see [[concepts/MinimalAPIArchitecture]])

## Project Structure

```
LlaModem/
├── Config/           — Configuration records (AppConfig, RouterConfig, ModelConfig, UsageConfig)
├── Middleware/       — ASP.NET Core middleware (auth, logging, usage capture)
├── Models/           — DTOs and response models
├── Services/         — Core business logic and infrastructure services
├── Utilities/        — Extension methods and helpers
└── Program.cs        — DI container setup, middleware pipeline, endpoint configuration
```

## Key Components

### Model Lifecycle Management (see [[concepts/ModelLifecycleManagement]])
- **ModelManager**: Orchestrates lifecycle operations (lazy start, hot switching, idle shutdown)
- **DefaultModelLauncher**: Launches PowerShell processes for llama-server backends
- **HealthChecker**: Polls `/health` endpoint until backend ready
- **IdleTimeoutService**: Monitors inactivity and triggers auto-shutdown

### Request Forwarding (see [[entities/RequestForwarder]])
- Proxies requests to llama-server backends via `HttpClient`
- Injects HTTP headers into JSON body at root level (see [[concepts/HeaderValueInjectionPattern]])
- Handles error responses with structured JSON errors

### Usage Tracking (see [[concepts/UsageTrackingPattern]])
- **ResponseUsageMiddleware**: Captures non-streaming response bodies
- **UsageExtractor**: Parses token usage from llama-server JSON responses
- **UsageService**: Persists session entries to SQLite via Dapper
- **StatsService**: Aggregates usage data for admin endpoints

### Admin Dashboard (see [[entities/DashboardService]])
- `/health` — Router health + active model name
- `/admin/status` — Current model state and backend URL
- `/admin/model` — Switch to specified model
- `/admin/stop` — Stop currently running model
- `/admin/stats/*` — Usage statistics and cost comparison

## Middleware Pipeline

For `/v1/*` proxy routes:
1. `ResponseUsageMiddleware` — Captures response body for usage stats
2. `RequestLoggingMiddleware` — Logs request details (debug level)
3. `BasicAuthMiddleware` — Validates Basic Auth credentials
4. Endpoint handlers — Forward to backend via `RequestForwarder`

**Note:** Admin endpoints (`/admin/*`, `/health`) bypass auth middleware.

## Configuration Pattern

Immutable POCO records bound via `IOptions<T>`:
- **AppConfig**: Root config containing all sections
- **RouterConfig**: Router settings (URL, auth, timeouts, header injection)
- **ModelConfig**: Per-model configuration (start script, backend URL)
- **UsageConfig**: Usage tracking settings (enabled flag, database path)

Environment variables expand at startup:
```bash
LLAMODEM_AUTH_USERNAME=admin
LLAMODEM_AUTH_PASSWORD=secret
QWEN_SMART_START_SCRIPT=/path/to/script.ps1
```

## API Endpoints

### Proxy (`/v1/*` — authenticated)
- `POST /v1/chat/completions` — Chat completion (OpenAI-compatible)
- `POST /v1/completions` — Legacy completions endpoint

**Required headers:**
- `X-Llama-Model` — Target model name (selects backend config)
- `Authorization: Basic <base64>` — Username:password credentials

### Admin (`/admin/*`, `/health` — unauthenticated)
- `GET /health` — Router health + active model name
- `GET /admin/status` — Current active model and backend URL
- `POST /admin/model` — Switch to specified model (`{ "model": "name" }`)
- `POST /admin/stop` — Stop the currently running model

### Usage Stats (`/admin/stats/*` — unauthenticated)
- `GET /admin/stats/usage` — Daily aggregated usage (`?days=30&model=name`)
- `GET /admin/stats/requests` — Paginated recent requests (`?limit=50&offset=0`)
- `GET /admin/stats/cost-comparison` — Cloud cost estimates for same token usage

## Model Lifecycle Phases

1. **Lazy Start** — Models only launch when first requested (no pre-loading)
2. **Hot Switching** — Request different model → current stops, new starts
3. **Idle Shutdown** — Stops active model after `IdleTimeoutSeconds` (default: 600s) of inactivity
4. **Health Checks** — Poll llama-server `/health` endpoint until healthy or timeout (5 min max)
5. **Process Cleanup** — On app shutdown, all tracked PowerShell processes terminated gracefully

## Data Persistence

### SQLite Database (`usage/usage.db`)
Table: `session_entries`
- Timestamp, model name, route, token counts (prompt/completion/total)
- Optional timing data (prompt_ms, completion_ms, cache_hits)
- Client IP, status code, request headers

**Indexes:** On `model` and `timestamp` for efficient queries.

### In-Memory Metrics
`ModelMetricsService` caches aggregated token usage for fast admin endpoint responses.

## Error Handling Pattern

Structured errors via `ErrorResponseWriter.WriteAsync()`:
```json
{
  "error": {
    "code": "BACKEND_UNAVAILABLE",
    "message": "llama-server backend not responding"
  }
}
```

**Error codes:** `MODEL_NOT_FOUND`, `BACKEND_UNAVAILABLE`, `INVALID_REQUEST`, `INTERNAL_ERROR`, `UNAUTHORIZED`.

## External Dependencies

| Dependency | Purpose |
|------------|---------|
| `llama-server` | LLM backend (runs GGUF models via CLI) |
| SQLite / Dapper | Usage statistics persistence |
| Serilog | Structured logging (console + file sinks) |
| Scalar.AspNetCore | OpenAPI UI / API reference |
| System.Management | GPU memory inspection via nvidia-smi |

## Security Considerations

- **Basic Auth only**: No Bearer tokens, OAuth, or API keys supported
- **Admin endpoints open**: `/admin/*` routes bypass auth — intended for trusted environments only
- **No HTTPS**: Basic Auth credentials sent in plaintext (requires reverse proxy with TLS)
- **Local network exposure**: Not designed for public internet deployment

## Related Entities

- [[entities/ModelManager]] - Orchestrates model lifecycle operations
- [[entities/RequestForwarder]] - Proxies requests to llama-server backends
- [[entities/ResponseUsageMiddleware]] - Captures token usage statistics
- [[entities/DashboardService]] - Admin endpoint handlers
- [[entities/StatsService]] - Usage aggregation and cost comparison
- [[entities/DefaultModelLauncher]] - PowerShell process management
- [[entities/HealthChecker]] - Backend health polling service
- [[entities/IdleTimeoutService]] - Inactivity monitoring and shutdown trigger
- [[entities/UsageExtractor]] - Token usage parsing utility
- [[entities/HeaderValueInjector]] - HTTP header to JSON mapping service
- [[entities/ConfigRecords]] - Immutable configuration records

## Related Concepts

- **ModelLifecycleManagement** — Automatic model lifecycle pattern
- **UsageTrackingPattern** — Token usage capture and persistence pattern
- **HeaderValueInjectionPattern** — Header-to-body mapping with type inference
- **MinimalAPIArchitecture** — Minimal API with extension methods pattern
