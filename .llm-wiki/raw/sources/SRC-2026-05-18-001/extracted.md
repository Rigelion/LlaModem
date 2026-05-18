# LlaModem Architecture Guidance — Summary

## Overview

This directory contains comprehensive architecture documentation for the llamodem repository, designed to help AI assistants understand and work with the codebase effectively.

## Documentation Structure

### Root Level

| File | Purpose | Lines |
|------|---------|-------|
| `architecture.md` | Project overview, key components, workflows | ~100 |
| `SUMMARY.md` | This file — navigation aid | ~50 |

### Config Layer (`.rpiv/guidance/Config/`)

| File | Purpose | Lines |
|------|---------|-------|
| `architecture.md` | Configuration records, binding patterns, environment variables | ~80 |

**Key concepts:**
- Immutable POCO records (`AppConfig`, `RouterConfig`, `ModelConfig`, `UsageConfig`)
- Options pattern with `IOptions<T>` and `ValidateOnStart()`
- Environment variable expansion for script paths

### Services Layer (`.rpiv/guidance/Services/`)

| File | Purpose | Lines |
|------|---------|-------|
| `architecture.md` | Service layer overview, interface patterns, dependencies | ~100 |
| `ModelManager.md` | Model lifecycle: lazy start, hot switch, idle shutdown | ~80 |
| `DashboardService.md` | Admin endpoints (status, stats, model control) | ~80 |
| `UsageService.md` | Usage tracking, SQLite persistence, cost comparison | ~90 |
| `RequestForwarder.md` | HTTP proxy logic, header injection, error handling | ~80 |
| `DefaultModelLauncher.md` | PowerShell process launch, health polling | ~60 |
| `HeaderValueInjector.md` | HTTP header to JSON body mapping, type conversion | ~90 |
| `UsageExtractor.md` | Token usage parsing from llama-server responses | ~90 |

**Key concepts:**
- Interface-first pattern (`I*` interfaces + concrete implementations)
- Singleton DI registration in `Program.cs`
- State repository pattern for process tracking

### Middleware Layer (`.rpiv/guidance/Middleware/`)

| File | Purpose | Lines |
|------|---------|-------|
| `ResponseUsageMiddleware.md` | Usage capture, response buffering, streaming detection | ~90 |
| `RequestLoggingMiddleware.md` | Request details logging (debug level) | ~50 |
| `BasicAuthMiddleware.md` | Basic auth validation on `/v1/*` routes | ~80 |

**Key concepts:**
- Middleware pipeline ordering and responsibilities
- Response body buffering for usage capture
- Streaming vs non-streaming response handling

### Test Project (`.rpiv/guidance/LlaModem.Tests/`)

| File | Purpose | Lines |
|------|---------|-------|
| `architecture.md` | Testing strategy, mock patterns, coverage focus | ~100 |

**Key concepts:**
- xUnit test framework
- In-memory mocks (`InMemoryModelRepository`, fake persistence)
- Service logic and middleware behavior testing

### PowerShell Scripts (`.rpiv/guidance/powershell/`)

| File | Purpose | Lines |
|------|---------|-------|
| `README.md` | Script template, launch parameters, deployment workflow | ~100 |

**Key concepts:**
- Model launch script structure and conventions
- Launch parameters (Temperature, TopP, PresencePenalty)
- Port 8001 sequential execution model

## How to Use This Documentation

### For AI Assistants

1. **Start with root `architecture.md`**: Get project overview and key components
2. **Navigate to specific layers**: Use subfolder files for detailed patterns
3. **Follow cross-references**: Subfolder files reference each other via `.rpiv/guidance/...` paths
4. **Check workflows**: Look for "Workflow" sections in service documentation

### For Developers

1. **Adding a new model**: Read `powershell/README.md` + `Services/ModelManager.md`
2. **Adding admin endpoints**: Read `Services/DashboardService.md`
3. **Adding middleware**: Read `Middleware/architecture.md` (if created) + existing examples
4. **Writing tests**: Read `.rpiv/guidance/LlaModem.Tests/architecture.md`

## Key Architectural Patterns

### 1. Layered Architecture with DI

```
Config/ → Services/ → Middleware/ → Endpoints
```

- Configuration records bound via `IOptions<T>`
- Services registered as singletons in `Program.cs`
- Middleware pipeline configured in `Program.cs`

### 2. Interface-First Service Design

```csharp
public interface IModelRepository { ... }
public class InMemoryModelRepository : IModelRepository { ... }

// Registration:
builder.Services.AddSingleton<IModelRepository, InMemoryModelRepository>();
```

### 3. Minimal API with Extension Methods

```csharp
// Program.cs:
app.MapStatsEndpoints(); // Extension method

// StatsEndpointExtensions.cs:
public static WebApplication MapStatsEndpoints(this WebApplication app) { ... }
```

### 4. Usage Capture Pipeline

```
Response body buffered → JSON parsed → SessionEntry created → SQLite persisted
```

### 5. Model Lifecycle State Management

```csharp
IModelRepository stores ModelProcessState (modelName, processId, startedAt)
ModelManager orchestrates: lazy start → health check → running → idle shutdown
```

## Common Workflows

### Adding a New Service

1. Create `INewService` interface in `Services/`
2. Create `NewService` implementation in `Services/`
3. Register in `Program.cs`: `builder.Services.AddSingleton<INewService, NewService>()`
4. Inject via constructor in consuming classes

### Adding a New Model

1. Add config to `appsettings.json`: `"modelName": { "StartScript": "...", "BackendUrl": "..." }`
2. Create PowerShell script in `powershell/` directory (see existing scripts)
3. Set environment variable for script path: `MODEL_NAME_START_SCRIPT="C:\scripts\script.ps1"`

### Adding an Admin Endpoint

1. Add method to `DashboardService`
2. Call `app.MapStatsEndpoints()` extension method in `Program.cs`
3. Use `[HttpGet]` / `[HttpPost]` attributes with route templates

### Writing Tests

1. Create test class in `LlaModem.Tests/`: `[ClassName]Tests.cs`
2. Use in-memory mocks (`InMemoryModelRepository`, fake persistence)
3. Test service logic and middleware behavior (not endpoint routing)

## External Integrations

| Dependency | Purpose | Documentation |
|------------|---------|--------------|
| `llama-server` | LLM backend (GGUF models) | `powershell/README.md` |
| SQLite / Dapper | Usage persistence | `Services/UsageService.md` |
| Serilog | Structured logging | `Program.cs` + `appsettings.json` |
| Scalar.AspNetCore | OpenAPI UI | `OpenApiDocumentTransformer.cs` |

## File Locations Reference

```
/Users/rigel/RiderProjects/llamodem/
├── .rpiv/guidance/
│   ├── architecture.md                    # Root overview
│   ├── Config/
│   │   └── architecture.md               # Configuration records
│   ├── Services/
│   │   ├── architecture.md              # Service layer overview
│   │   ├── ModelManager.md              # Lifecycle management
│   │   ├── DashboardService.md          # Admin endpoints
│   │   ├── UsageService.md              # Usage tracking
│   │   ├── RequestForwarder.md          # HTTP proxy
│   │   ├── DefaultModelLauncher.md      # Process launcher
│   │   ├── HeaderValueInjector.md       # Header to JSON mapping
│   │   └── UsageExtractor.md            # Token usage parsing
│   ├── Middleware/
│   │   ├── ResponseUsageMiddleware.md   # Usage capture
│   │   ├── RequestLoggingMiddleware.md  # Request logging
│   │   └── BasicAuthMiddleware.md       # Auth validation
│   ├── LlaModem.Tests/
│   │   └── architecture.md              # Testing patterns
│   └── powershell/
│       └── README.md                    # Script conventions
├── Config/                               # Source config records
├── Middleware/                           # Source middleware
├── Services/                             # Source services
├── LlaModem.Tests/                       # Test project
└── powershell/                           # Model launch scripts
```

## Maintenance Notes

- **Line limit**: Subfolder files target ~100 lines (code examples essential)
- **No frontmatter**: Pure markdown, no YAML headers
- **Cross-references**: Use `.rpiv/guidance/...` paths for internal links
- **Conditional sections**: `<important if="...">` blocks for scenario-specific recipes

## Contributing to Documentation

When adding new features:

1. Update relevant architecture.md file(s)
2. Add code examples for new patterns
3. Document configuration options
4. Include workflow checklists for common tasks
5. Keep prose concise — LLMs are in-context learners

---

**Generated**: 2026-05-18  
**Project**: llamodem (net10.0-windows)  
**Purpose**: AI assistant guidance for codebase understanding and development workflows
