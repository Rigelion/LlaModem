# Phase 4: Request Routing & Idle Timeout

## Goal
Wire up the OpenAI-compatible proxy route with model selection header support and implement idle shutdown timeout.

**All tasks complete ✅**

## Tasks

### 4.1 Model Selection Endpoint
- [x] Create a route for `/v1/**` in `Program.cs`
- [x] Read the `X-Llama-Model` header from each request in the proxy handler
- [x] If the header is missing, return `400 Bad Request` with an error message explaining required header
- [x] If the model name is not in config, return `400 Bad Request` with a list of available models

### 4.2 Ensure Model is Running
- [x] Before forwarding, call `ModelManager.EnsureModelAsync(modelName)`:
  - If the requested model is already active, do nothing
  - If a different model is active, switch to the requested one (stop current → start new → health check)
  - If no model is active, start the requested one

### 4.3 Request Forwarding
- [x] Forward the incoming request to the active model's `BackendUrl`:
  - Strip `/v1` prefix, preserve the rest of the path (e.g., `/chat/completions`)
  - Forward the request body as a stream
  - Forward all headers except hop-by-hop headers (`Host`, `Connection`, etc.)
  - Use `HttpClient` with 5-minute timeout for streaming responses
- [x] Stream the response back to the client via `CopyToAsync`
- [x] Preserve HTTP status codes from the backend

### 4.4 Idle Timeout Shutdown
- [x] Create `Services/IdleTimeoutService.cs`:
  - Register as a `BackgroundService` (`IHostedService`)
  - Track last request time via `IRequestTracker` — updated on every `/v1/*` request
  - On each tick (every 30 seconds), compare elapsed time since last request to `IdleTimeoutSeconds`
  - If timeout exceeded, call `ModelManager.StopActiveModelAsync()` and log the shutdown

### 4.5 Last Request Tracking
- [x] Create `Services/RequestTracker.cs` with `IRequestTracker` interface:
  - Thread-safe timestamp update using `lock`
  - Exposed to `IdleTimeoutService` via DI

## Notes
- Streaming responses (SSE) must be supported — use `HttpCompletionOption.ResponseHeadersRead`
- The idle timeout applies globally — all models are stopped after inactivity
- ngrok is out of scope — the router only exposes `localhost:9000`
