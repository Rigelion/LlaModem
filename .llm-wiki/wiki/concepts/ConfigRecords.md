---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-002]]
  - [[sources/SRC-2026-05-18-003]]
status: complete
---

# ConfigRecords

**Description:** Immutable configuration records using the Options pattern (`IOptions<T>`). All config types are POCO records with no behavior, bound from `appsettings.json` at startup.

## Configuration Hierarchy

- **AppConfig** — Root config containing all sections (Models dictionary, BackendUrl)
- **RouterConfig** — Router settings (listen URL, auth credentials, timeouts, header injection mappings)
- **ModelConfig** — Per-model configuration (start script path, backend URL override, exclusive flag)
- **UsageConfig** — Usage tracking settings (enabled flag, SQLite database path)

## Key Patterns

### Options Pattern with Validation
```csharp
builder.Services.Configure<AppConfig>(
    builder.Configuration.GetSection(AppConfig.SectionName));

builder.Services.Configure<RouterConfig>(
    builder.Configuration.GetSection(RouterConfig.SectionName))
    .ValidateOnStart(); // Fail-fast if config invalid
```

### Environment Variable Expansion
```csharp
// In Program.cs after Configure<AppConfig>():
var appConfig = serviceProvider.GetRequiredService<AppConfig>();
foreach (var model in appConfig.Models.Values)
{
    model.StartScript = Environment.ExpandEnvironmentVariables(model.StartScript);
}
```

**Note:** Expansion happens at startup, not per-request. Scripts reference expanded paths directly.

## Configuration Sources

| Source | Priority |
|--------|----------|
| Environment variables (`LLAMODEM_AUTH_USERNAME`, etc.) | Higher |
| `appsettings.json` | Lower |
| Default values | Lowest |

**Example:** Auth credentials prefer environment variables over JSON config.

## Related Entities

- [[entities/ConfigRecords]] - Immutable configuration records
- [[entities/ModelManager]] - Model lifecycle orchestration
- [[entities/RequestForwarder]] - Proxy with header injection
- [[entities/ResponseUsageMiddleware]] - Usage capture middleware
- [[entities/BasicAuthMiddleware]] - Basic authentication middleware
- [[entities/DefaultModelLauncher]] - PowerShell process launcher
- [[entities/IdleTimeoutService]] - Idle timeout monitoring

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
