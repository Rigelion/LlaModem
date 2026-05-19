# Configuration Architecture

**Location**: `Config/` directory  
**Pattern**: Immutable POCO records with Options pattern (`IOptions<T>`)

## Configuration Records

| Record | Purpose | Key Properties |
|--------|---------|----------------|
| `AppConfig` | Root config, aggregates all sections | `Models`, `BackendUrl` |
| `RouterConfig` | Router settings | `ListenUrl`, `AuthUsername`, `Timeouts` |
| `ModelConfig` | Per-model configuration | `StartScript`, `BackendUrl`, `Exclusive` |
| `UsageConfig` | Usage tracking settings | `Enabled`, `Path` |

## Header Injection Removal (2026-05-19)

**Removed options**:
- `RouterConfig.EnableBodyHeaderInjection` — No longer exists
- `RouterConfig.BodyHeaderMappings` — Removed entirely

**New parameter strategy**: Model launch parameters loaded from `dashboard_params.json` via `DashboardService.LoadParams()`.

## Related Components

- [[Services/ModelProxyHandler]] — Loads params on each request
- [[Services/DashboardService]] — Parameter persistence and loading