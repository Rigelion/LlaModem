# LlaModem

A lightweight router for local LLM servers that exposes an OpenAI-compatible API. Routes requests to one of several `llama-server` backends based on the `X-Llama-Model` header, with automatic model lifecycle management (lazy start, idle shutdown, hot switching).

## Quick Start

```bash
dotnet restore
dotnet build
dotnet run
```

The server listens on `http://localhost:9000` (configured in `appsettings.json`).

## Usage

### Basic Request

```bash
curl -u admin:your-password \
  -H "X-Llama-Model: qwen-smart" \
  -H "Content-Type: application/json" \
  -d '{"model":"qwen-smart","messages":[{"role":"user","content":"Hello"}]}' \
  http://localhost:9000/v1/chat/completions
```

### Model Parameters via Headers

Pass generation parameters as HTTP headers — LlaModem injects them into the request body:

```bash
curl -u admin:your-password \
  -H "X-Llama-Model: qwen-smart" \
  -H "X-Llama-Temperature: 0.7" \
  -H "X-Llama-TopP: 0.9" \
  -H "X-Llama-PresencePenalty: -0.5" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Hello"}]}' \
  http://localhost:9000/v1/chat/completions
```

See `BodyHeaderMappings` in the configuration section for the full list of supported headers.

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
    "IdleTimeoutSeconds": 600,
    "EnableBodyHeaderInjection": true,
    "BodyHeaderMappings": {
      "x-client-id": "clientId",
      "x-debug": "debug",
      "x-llama-temperature": "temperature",
      "X-Llama-TopP": "top_p",
      "X-Llama-PresencePenalty": "presence_penalty"
    },
    "Timeouts": {
      "VramThresholdGb": 12,
      "NvidiaSmiTimeoutSeconds": 5,
      "GracefulShutdownTimeoutSeconds": 5,
      "HealthCheckTimeoutSeconds": 3,
      "HealthCheckPollTimeoutMinutes": 5,
      "HealthCheckPollDelayMs": 500,
      "IdleCheckIntervalSeconds": 30
    }
  },
  "Models": {
    "qwen-smart": { "StartScript": "%QWEN_SMART_START_SCRIPT%", "BackendUrl": "http://localhost:8001" },
    "qwen-fast": { "StartScript": "%QWEN_FAST_START_SCRIPT%", "BackendUrl": "http://localhost:8002" }
  }
}
```

### Configuration Notes

- **Environment variables**: Model start scripts can reference environment variables using `%VAR_NAME%` syntax (expanded at startup). Set them via `LLAMODEM_AUTH_USERNAME`, `LLAMODEM_AUTH_PASSWORD`, `QWEN_SMART_START_SCRIPT`, `QWEN_FAST_START_SCRIPT`, or `ASPNETCORE_ENVIRONMENT`.
- **Header injection**: When enabled, LlaModem reads configured HTTP headers and injects their values into the JSON request body at the root level. Values are auto-typed (boolean, integer, double, or string).
- **Idle timeout**: The active model shuts down automatically after `IdleTimeoutSeconds` of no requests.
- **Timeouts**: All thresholds in `Timeouts` are configurable per deployment — VRAM requirements, health check timing, graceful shutdown, and idle check interval.

## PowerShell Start Scripts

Each model in `appsettings.json` references a `.ps1` script via `StartScript`. LlaModem launches this script with optional launch parameters (temperature, top_p, presence_penalty) passed as command-line arguments.

### Script Template

```powershell
param(
    [double]$Temperature = 0.6,
    [double]$TopP = 0.95,
    [double]$PresencePenalty = 0.00
)

# Set cache and environment paths
$env:TEMP = "F:\Temp"
$env:LLAMA_CACHE = "F:\llama-cache"
$env:HF_HOME = "F:\hf-cache"

# Create directories if they don't exist
New-Item -ItemType Directory -Force F:\Temp, F:\llama-cache, F:\hf-cache | Out-Null

# Display configuration
Write-Host "`n=== Model Configuration ===" -ForegroundColor Cyan
Write-Host "Port:              8001"
Write-Host "Temperature:       $Temperature"
Write-Host "TopP:              $TopP"
Write-Host "Model Path:        <path to .gguf>"
Write-Host "========================================`n" -ForegroundColor Cyan

# Start llama-server
llama-server `
    --model "<path to model.gguf>" `
    --port 8001 `
    -c 131072 `
    -n 4096 `
    --threads 6 `
    --temp $Temperature `
    --top-p $TopP `
    --presence-penalty $PresencePenalty
```

### Key Points

- **Parameters**: LlaModem passes launch parameters as PowerShell arguments (`-Temperature`, `-TopP`, `-PresencePenalty`). These are only used on the first start — subsequent requests to the same running model ignore them.
- **Environment variables**: Set paths, cache locations, and other config via `$env:` variables inside the script. LlaModem expands `%VAR%` references in `StartScript` paths at startup.
- **Port assignment**: Each model needs its own port. LlaModem routes based on the `BackendUrl` configured for each model.
- **Health endpoint**: llama-server exposes `/health` by default — LlaModem polls this to confirm the model is ready.

## Architecture

```
Client → LlaModem (:9000) → llama-server backend (:8001 / :8002)
```

### Request Flow

1. **Request Logging** — logs method, path, headers, and body (debug level)
2. **Basic Auth** — validates credentials on `/v1/*` routes only
3. **Header Injection** — maps HTTP headers to JSON body fields (configurable)
4. **Model Routing** — selects the target backend via `X-Llama-Model` header
5. **Lazy Start** — model auto-starts on first request if not running
6. **Forwarding** — proxies request to the selected `llama-server` backend

### Model Lifecycle

- **Lazy start**: Models only launch when first requested
- **Hot switching**: Request a different model — the current one stops, the new one starts
- **Idle shutdown**: Stops the active model after `IdleTimeoutSeconds` of inactivity
- **VRAM check**: Validates available GPU memory before launching (requires `nvidia-smi`)
- **Graceful shutdown**: 5-second graceful stop → force kill fallback on exit
- **Process tree cleanup**: On application shutdown, all tracked PowerShell processes are terminated

## Tech Stack

C# / .NET 10 · ASP.NET Core Minimal API · Kestrel · Serilog · Windows PowerShell
