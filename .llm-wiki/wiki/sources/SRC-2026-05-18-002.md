# LlaModem — Root Architecture Overview

## Project Purpose

LlaModem is a lightweight router for local LLM servers that exposes an OpenAI-compatible API. It routes requests to one of several `llama-server` backends based on the `X-Llama-Model` header, with automatic model lifecycle management (lazy start, idle shutdown, hot switching).

## Project Map

```
LlaModem (net10.0-windows)
├── Config/           — Configuration records (AppConfig, RouterConfig, ModelConfig, UsageConfig)
├── Middleware/       — ASP.NET Core middleware (auth, logging, usage capture)
├── Models/           — DTOs and response models
├── Services/         — Core business logic and infrastructure services
├── Utilities/        — Extension methods and helpers
├── LlaModem.Tests/  — Unit tests
└── Program.cs        — DI container setup, middleware pipeline, endpoint configuration
```

## Architecture Pattern

Layered architecture with dependency injection:
- **No strict Clean Architecture** — single project structure
- **Service-layer organization** — interfaces + concrete implementations in `Services/`
- **Configuration records** — immutable POCO records in `Config/`
- **Minimal API pattern** — ASP.NET Core Minimal API with extension methods

## Key Components

| Component | Responsibility |
|-----------|--------------|
| `Program.cs` | DI container setup, middleware pipeline configuration, endpoint mapping |
| `ModelManager` | Model lifecycle: lazy start, hot switching, idle shutdown, health checks |
| `RequestForwarder` | Proxies requests to llama-server backends with header injection |
| `ResponseUsageMiddleware` | Captures token usage stats from non-streaming responses |
| `StatsService` / `UsageService` | Usage tracking and statistics aggregation (SQLite-backed) |
| `DashboardService` | Admin endpoints for status, model switching, usage stats |

## Configuration

All configuration uses `IOptions<T>` pattern with records:
- `AppConfig` — Root config containing all sections
- `RouterConfig` — Router settings (URL, auth, timeouts, header injection)
- `ModelConfig` — Per-model configuration (start script, backend URL)
- `UsageConfig` — Usage tracking settings (enabled, database path)

See `.rpiv/guidance/Config/architecture.md` for details.

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run
```

Environment variables expand model start scripts at startup:
- `LLAMODEM_AUTH_USERNAME`, `LLAMODEM_AUTH_PASSWORD` — Basic auth credentials
- `QWEN_SMART_START_SCRIPT`, etc. — Model launch script paths
- `ASPNETCORE_ENVIRONMENT` — Development environment

## API Endpoints

### Proxy (authenticated, `/v1/*`)
- `POST /v1/chat/completions` — Chat completion (OpenAI-compatible)
- `POST /v1/completions` — Legacy completions endpoint

**Headers:**
- `X-Llama-Model` — Required: selects target model
- `X-Llama-*` — Optional: temperature, top_p, presence_penalty, etc.

### Admin (unauthenticated)
- `GET /health` — Router health + active model name
- `GET /admin/status` — Current active model and backend URL
- `POST /admin/model` — Switch to specified model (`{ "model": "name" }`)
- `POST /admin/stop` — Stop the currently running model

### Usage Stats (unauthenticated)
- `GET /admin/stats/usage` — Daily aggregated usage (`?days=30&model=name`)
- `GET /admin/stats/requests` — Paginated recent requests (`?limit=50&offset=0`)
- `GET /admin/stats/cost-comparison` — Cloud cost estimates for same token usage

See `.rpiv/guidance/Services/DashboardService.md` for admin endpoint details.

## Model Lifecycle

1. **Lazy start** — Models only launch when first requested
2. **Hot switching** — Request a different model → current stops, new starts
3. **Idle shutdown** — Stops active model after `IdleTimeoutSeconds` (default: 600s) of inactivity
4. **Health checks** — Poll llama-server `/health` endpoint until healthy or timeout
5. **Process cleanup** — On app shutdown, all tracked PowerShell processes terminated

See `.rpiv/guidance/Services/ModelManager.md` for lifecycle details.

## Usage Statistics

Persisted to SQLite database at `usage/usage.db`:
- Timestamp, model name, route, token counts (prompt/completion/total)
- Optional timing data (prompt_ms, completion_ms, cache_hits)
- Client IP, status code, request headers

Query with: `sqlite3 usage/usage.db`

See `.rpiv/guidance/Services/UsageService.md` for usage tracking details.

## Cross-Layer Workflows

### Adding a New Model
1. Add model config to `appsettings.json`: `"modelName": { "StartScript": "...", "BackendUrl": "..." }`
2. Create PowerShell launch script in `powershell/` directory (see existing scripts)
3. Ensure script uses port 8001 (only one model runs at a time)

See `.rpiv/guidance/powershell/README.md` for script template.

### Adding a New Admin Endpoint
1. Add method to `DashboardService`
2. Call `app.MapStatsEndpoints()` in `Program.cs` — uses extension method pattern
3. Use `[HttpGet]` / `[HttpPost]` attributes with route templates

See `.rpiv/guidance/Services/DashboardService.md`.

### Adding a New Service
1. Create interface: `INewService` (PascalCase, no `I` prefix on impl)
2. Create concrete implementation: `NewService`
3. Register in `Program.cs`: `builder.Services.AddSingleton<INewService, NewService>()`
4. Inject via constructor in consuming classes

See `.rpiv/guidance/Services/architecture.md`.

## Testing Strategy

- **xUnit** test project (`LlaModem.Tests`)
- **In-memory mocks**: `InMemoryModelRepository`, fake HTTP clients
- **Focus areas**: service logic, middleware behavior, utility functions
- **Not tested**: endpoint routing (covered by integration tests in Minimal API pattern)

See `.rpiv/guidance/LlaModem.Tests/architecture.md`.

## External Integrations

| Dependency | Purpose |
|------------|---------|
| `llama-server` | LLM backend (runs GGUF models via CLI) |
| SQLite / Dapper | Usage statistics persistence |
| Serilog | Structured logging (console + file sinks) |
| Scalar.AspNetCore | OpenAPI UI / API reference |
| System.Management | GPU memory inspection via nvidia-smi |

## Development Notes

- **PowerShell scripts**: Model launchers use `%VAR%` syntax for env var expansion at runtime
- **Port assignment**: All models share port 8001 (sequential, not concurrent)
- **Header injection**: HTTP headers mapped to JSON body fields via `HeaderValueInjector`
- **Timeouts**: Configurable per-service (health check poll interval, idle threshold, graceful shutdown)
