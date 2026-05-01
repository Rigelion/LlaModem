# LlaModem — Local Llama Router

## Overview

A local Windows application that exposes an OpenAI-compatible API through a custom router. Requests are routed to one of several local `llama-server` models based on the `X-Llama-Model` header.

## Tech Stack

- **Language:** C# / .NET 10 (net10.0)
- **Framework:** ASP.NET Core Minimal API
- **Hosting:** Console-hosted (Kestrel)
- **Auth:** HTTP Basic Authentication
- **Target OS:** Windows

## Architecture

```
Client → ngrok → LlaModem Router (:9000) → llama-server backend (:8001 / :8002)
```

The router:
1. Authenticates requests via Basic Auth on `/v1/*` routes
2. Reads `X-Llama-Model` header to select the target model
3. Starts/stops the selected model's backend on demand
4. Forwards OpenAI-compatible requests unchanged to the active backend
5. Shuts down the idle model after a configurable timeout

## Supported Models

| Model Name  | Backend URL        | Start Script                        |
|-------------|--------------------|-------------------------------------|
| `qwen-smart`| `http://localhost:8001` | `F:\llama\start-qwen-smart.ps1`  |
| `qwen-fast` | `http://localhost:8002` | `F:\llama\start-qwen-fast.ps1`   |

## Configuration

All configuration lives in `appsettings.json`:

```json
{
  "Router": {
    "ListenUrl": "http://localhost:9000",
    "AuthUsername": "admin",
    "AuthPassword": "change-me"
  },
  "Usage": {
    "Enabled": true,
    "Path": "usage"
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

## Endpoints

### Proxy (authenticated)

| Method | Path       | Headers              | Description                              |
|--------|------------|----------------------|------------------------------------------|
| POST   | `/v1/**`   | `X-Llama-Model`      | Forward OpenAI-compatible requests       |

### Admin (unauthenticated)

| Method | Path             | Body / Params        | Description                      |
|--------|------------------|----------------------|----------------------------------|
| GET    | `/health`        | —                    | Router health + active model     |
| GET    | `/admin/status`  | —                    | Current active model info        |
| POST   | `/admin/model`   | `{ "model": "..." }` | Switch to specified model        |
| POST   | `/admin/stop`    | —                    | Stop the active model            |

## Project Structure

```
LlaModem/
├── Config/
│   ├── AppConfig.cs            # Root config (Models section)
│   ├── ModelConfig.cs          # Individual model config (backend URL, start script)
│   ├── ProxyHeaders.cs         # Standard proxy header constants
│   └── RouterConfig.cs         # Router settings + timeout thresholds
├── Middleware/
│   ├── BasicAuthExtensions.cs       # Extension for conditional auth middleware
│   ├── BasicAuthMiddleware.cs       # HTTP Basic Auth implementation
│   ├── RequestLoggingExtensions.cs  # Logging pipeline registration
│   ├── RequestLoggingMiddleware.cs  # Request/response logging middleware
│   ├── UsageCaptureExtensions.cs    # Usage statistics middleware registration
│   └── UsageCaptureMiddleware.cs    # Token usage extraction from proxy responses
├── Models/
│   ├── SessionEntry.cs       # Per-request usage data record
│   └── TokenUsage.cs         # Token count record (prompt/completion/total)
├── Services/
│   ├── DefaultModelLauncher.cs      # PowerShell process launcher for models
│   ├── ErrorResponseWriter.cs        # JSON error response utility
│   ├── GpuMemoryChecker.cs          # VRAM availability check via nvidia-smi
│   ├── HeaderValueInjector.cs         # HTTP header → JSON body field injection
│   ├── HealthChecker.cs               # HTTP health endpoint polling
│   ├── HttpConstants.cs               # Excluded forwarded headers list
│   ├── IdleTimeoutService.cs        # Background service for idle model shutdown
│   ├── LaunchParamParser.cs           # Parses generation params from request headers
│   ├── ModelLaunchParams.cs         # Optional launch parameter record
│   ├── ModelManager.cs                # Active model lifecycle management
│   ├── ModelProxyHandler.cs          # /v1/ request routing handler
│   ├── ProcessKiller.cs               # Graceful process tree termination
│   ├── RequestForwarder.cs            # HTTP proxy to backend servers
│   ├── SystemIdleTracker.cs          # Tracks last-request timestamp (thread-safe)
│   └── UsageService.cs                # Persists token usage to daily markdown files
├── Utilities/
│   └── HttpRequestExtensions.cs  # Shared request body reading helper
├── Program.cs                    # Entry point, DI wiring, pipeline setup
├── EndpointSetup.cs              # Route registration (/v1/**, /health, /admin)
├── appsettings.json             # Configuration file
├── LlaModem.csproj              # Project file (.NET 10)
├── LlaModem.sln                 # Solution file
└── docs/
    ├── INIT.md                  # This file (project overview)
    └── powershell-parameters.md # Generation parameter documentation
```

## Key Behaviors

- **Model switching:** Serial — only one model runs at a time
- **Idle shutdown:** After `IdleTimeoutSeconds` of no requests, the active model is stopped
- **Graceful stop:** SIGTERM → wait 5s → force kill
- **Health check:** Polls `/health` on backend; retries every 500ms (configurable) until ready or timeout
- **Streaming:** SSE responses are streamed back (not buffered)

## Building & Running

```bash
dotnet restore
dotnet build
dotnet run
```

The router will listen on the URL configured in `appsettings.json` (default: `http://localhost:9000`).
