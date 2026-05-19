# Configuration Layer

## Responsibility

Immutable configuration records bound from `appsettings.json` using the Options pattern (`IOptions<T>`). All config types are POCO records with no behavior.

## Module Structure

```
Config/
├── AppConfig.cs          — Root config, aggregates all sections
├── RouterConfig.cs       — Router settings (URL, auth, timeouts)
├── ModelConfig.cs        — Per-model configuration (start script, backend URL)
└── UsageConfig.cs        — Usage tracking settings (enabled flag, database path)
```

## Configuration Records

### AppConfig

```csharp
public sealed record AppConfig
{
    public const string SectionName = "Router";
    
    public Dictionary<string, ModelConfig> Models { get; init; } = [];
    public string BackendUrl { get; init; } = "http://localhost:8001";
}
```

**Notes:**
- `Models` dictionary keyed by model name (matches `X-Llama-Model` header values)
- Default backend URL: port 8001 (single-model sequential execution)
- Environment variables expand `StartScript` paths at binding time via `Configure<AppConfig>` callback

### RouterConfig

```csharp
public sealed record RouterConfig
{
    public const string SectionName = "Router";
    
    public string ListenUrl { get; init; } = "http://localhost:9000";
    public string AuthUsername { get; init; } = "admin";
    public string AuthPassword { get; init; } = ""; // Set via LLAMODEM_AUTH_PASSWORD env var
    
    public TimeoutConfig Timeouts { get; init; } = new();
}

public sealed record TimeoutConfig
{
    public double VramThresholdGb { get; init; } = 12; // Deprecated: VRAM check disabled
    public int NvidiaSmiTimeoutSeconds { get; init; } = 5;
    public int GracefulShutdownTimeoutSeconds { get; init; } = 5;
    public int HealthCheckTimeoutMinutes { get; init; } = 5;
    public int HealthCheckPollDelayMs { get; init; } = 500;
    public int IdleTimeoutSeconds { get; init; } = 600; // Model auto-shutdown after inactivity
}
```

**Notes:**
- Auth credentials prefer environment variables (`LLAMODEM_AUTH_USERNAME`, `LLAMODEM_AUTH_PASSWORD`) over JSON config
- Idle timeout: 600s default — model stops after inactivity

### ModelConfig

```csharp
public sealed record ModelConfig
{
    public string StartScript { get; init; } = ""; // PowerShell script path with %VAR% syntax
    public string? BackendUrl { get; init; } // Override global backend URL per-model
    public bool Exclusive { get; init; } = false; // If true, blocks other models from running
}
```

**Notes:**
- `StartScript` uses PowerShell `%VAR_NAME%` syntax — expanded at startup via `Environment.ExpandEnvironmentVariables()`
- Scripts in `powershell/` directory use port 8001 (sequential model execution)
- `Exclusive` flag prevents hot-switching to this model while another is running

### UsageConfig

```csharp
public sealed record UsageConfig
{
    public const string SectionName = "Usage";
    
    public bool Enabled { get; init; } = true;
    public string Path { get; init; } = "usage/usage.db"; // SQLite database path
}
```

**Notes:**
- When enabled, all non-streaming proxy responses are parsed for token usage stats
- Stats persisted to SQLite via Dapper (see `DbSchema.cs` for table structure)
- Query with: `sqlite3 usage/usage.db`

## Parameter Loading Strategy

**Source**: `dashboard_params.json` (persisted via admin API)

**Flow**:
1. Client sends request with `X-Llama-Model` header
2. `ModelProxyHandler.LoadParamsForModel()` loads params from file
3. Model starts with loaded parameters (or defaults if file missing)
4. Parameters NOT injected from HTTP headers (removed in 2026-05-19)

**Important**: No header-to-body injection occurs — model parameters come solely from persisted JSON file.

## Configuration Binding Flow

```
appsettings.json 
  → IOptions<T> binding (ValidateOnStart)
  → Configure<AppConfig> callback expands %VAR% in StartScript paths
  → Injected into services via constructor
```

**Critical**: Environment variable expansion happens at startup, not per-request. Scripts reference expanded paths directly.

## Validation

- `ValidateOnStart()` ensures config binds correctly before app starts
- Missing required fields throw during DI container build (fail-fast)
- Default values provided for all optional fields

---

**Date**: 2026-05-19  
**Author**: Rigelion  
**Related Plan**: `.rpiv/artifacts/plans/2026-05-19_14-30-00_remove-header-body-injection.md`
