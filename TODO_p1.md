# Phase 1: Project Setup & Configuration

## Goal
Create a C# ASP.NET Core Minimal API console-hosted web app with project structure, configuration model, and build system.

## Tasks

### 1.1 Solution & Project Creation
- [x] Create `LlamaDem.sln` solution file
- [x] Create `LlamaDem.csproj` project file targeting .NET 8+ (net8.0) for Windows
- [x] Set output type to `Exe` (console-hosted web app)

### 1.2 Dependencies
- [x] Add `Microsoft.AspNetCore.OpenApi` package reference
- [x] Add `Swashbuckle.AspNetCore` for Swagger (dev-only, optional)
- [x] No external HTTP client needed — use built-in `HttpClient`

### 1.3 Configuration Model
- [x] Create `Config/RouterConfig.cs` with properties: `ListenUrl`, `AuthUsername`, `AuthPassword`, `IdleTimeoutSeconds`
- [x] Create `Config/ModelConfig.cs` with properties: `StartScript`, `BackendUrl`
- [x] Create `Config/AppConfig.cs` containing `Router` and dictionary of `Models` (keyed by model name)
- [x] Wire up `IConfiguration` to bind `appsettings.json` in `Program.cs`

### 1.4 appsettings.json
- [x] Create `appsettings.json` with the full config structure:
  ```json
  {
    "Router": {
      "ListenUrl": "http://localhost:9000",
      "AuthUsername": "admin",
      "AuthPassword": "change-me",
      "IdleTimeoutSeconds": 600
    },
    "Models": {
      "qwen-smart": {
        "StartScript": "F:\\llama\\start-qwen-smart.ps1",
        "BackendUrl": "http://localhost:8001"
      },
      "qwen-fast": {
        "StartScript": "F:\\llama\\start-qwen-fast.ps1",
        "BackendUrl": "http://localhost:8002"
      }
    }
  }
  ```

### 1.6 Git Initialization
- [ ] Initialize git repository (`git init`)
- [ ] Create `.gitignore` for .NET (bin/, obj/, user-secrets, IDE files)

### 1.5 Program.cs Entry Point
- [x] Create minimal `Program.cs` that builds and runs a `WebApplication`
- [x] Register config via `builder.Configuration`
- [x] Ensure app listens on the configured `ListenUrl`

## Notes
- Single project, single solution — Windows-first target
- No ngrok integration in this phase
- Config uses `appsettings.json` only (no environment variable overrides needed yet)
