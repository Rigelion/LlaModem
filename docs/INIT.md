# LlamaDem — Local Llama Router

## Overview

A local Windows application that exposes an OpenAI-compatible API through a custom router. Requests are routed to one of several local `llama-server` models based on the `X-Llama-Model` header.

## Tech Stack

- **Language:** C# / .NET 8 (net8.0)
- **Framework:** ASP.NET Core Minimal API
- **Hosting:** Console-hosted (Kestrel)
- **Auth:** HTTP Basic Authentication
- **Target OS:** Windows

## Architecture

```
Client → ngrok → LlamaDem Router (:9000) → llama-server backend (:8001 / :8002)
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
LlamaDem/
├── Config/
│   ├── AppConfig.cs          # Root config (Router + Models)
│   ├── ModelConfig.cs        # Individual model config
│   └── RouterConfig.cs       # Router settings
├── Middleware/
│   ├── BasicAuthExtensions.cs # Extension for auth middleware
│   └── BasicAuthMiddleware.cs # HTTP Basic Auth implementation
├── Services/
│   ├── ModelManager.cs        # Start/stop/switch models
│   ├── IdleTimeoutService.cs  # Background idle shutdown
│   └── RequestTracker.cs      # Last-request timestamp tracking
├── Program.cs                 # Entry point, route mapping, DI wiring
├── appsettings.json           # Configuration file
├── LlamaDem.csproj            # Project file
├── LlamaDem.sln               # Solution file
└── docs/
    └── INIT.md                # This file
```

## Key Behaviors

- **Model switching:** Serial — only one model runs at a time
- **Idle shutdown:** After `IdleTimeoutSeconds` of no requests, the active model is stopped
- **Graceful stop:** SIGTERM → wait 5s → force kill
- **Health check:** Polls `/health` on backend; retries every 500ms for up to 2 minutes
- **Streaming:** SSE responses are streamed back (not buffered)

## Building & Running

```bash
dotnet restore
dotnet build
dotnet run
```

The router will listen on the URL configured in `appsettings.json` (default: `http://localhost:9000`).
