# LlaModem

Local Llama model router that exposes an OpenAI-compatible API. Routes requests to one of several local `llama-server` backends based on the `X-Llama-Model` header.

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run
```

The server listens on `http://localhost:9000` (configured in `appsettings.json`).

## Usage

```bash
curl -u admin:your-password \
  -H "X-Llama-Model: qwen-smart" \
  -H "Content-Type: application/json" \
  -d '{"model":"qwen-smart","messages":[{"role":"user","content":"Hello"}]}' \
  http://localhost:9000/v1/chat/completions
```

## Endpoints

| Path | Auth | Description |
|------|------|-------------|
| `POST /v1/**` | Basic Auth | Proxy to selected model (`X-Llama-Model`) |
| `GET /health` | — | Router health + active model |
| `GET /admin/status` | — | Current active model info |
| `POST /admin/model` | — | Switch model: `{ "model": "qwen-smart" }` |
| `POST /admin/stop` | — | Stop the active model |

## Configuration

Edit `appsettings.json`:

```json
{
  "Router": {
    "ListenUrl": "http://localhost:9000",
    "AuthUsername": "admin",
    "AuthPassword": "your-password",
    "IdleTimeoutSeconds": 600
  },
  "Models": {
    "qwen-smart": { "StartScript": "F:\\llama\\start-qwen-smart.ps1", "BackendUrl": "http://localhost:8001" },
    "qwen-fast": { "StartScript": "F:\\llama\\start-qwen-fast.ps1", "BackendUrl": "http://localhost:8002" }
  }
}
```

## Architecture

```
Client → LlaModem (:9000) → llama-server backend (:8001 / :8002)
```

- Basic Auth on `/v1/*` routes
- Serial model loading — one model at a time
- Idle shutdown: stops the active model after `IdleTimeoutSeconds` of no requests
- Graceful stop (5s) → force kill fallback

## Tech Stack

C# / .NET 10 · ASP.NET Core Minimal API · Kestrel
