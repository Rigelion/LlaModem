# Phase 4: Request Routing & Idle Timeout

## Goal
Wire up the OpenAI-compatible proxy route with model selection header support and implement idle shutdown timeout.

## Tasks

### 4.1 Model Selection Endpoint
- [ ] Create a route group for `/v1/*` in `Program.cs`
- [ ] Add a middleware or handler that reads the `X-Llama-Model` header from each request
- [ ] If the header is missing, return `400 Bad Request` with an error message explaining required header
- [ ] If the model name is not in config, return `400 Bad Request` with a list of available models

### 4.2 Ensure Model is Running
- [ ] Before forwarding, call `ModelManager.StartModelAsync(modelName)`:
  - If the requested model is already active, do nothing
  - If a different model is active, switch to the requested one (stop current → start new → health check)
  - If no model is active, start the requested one

### 4.3 Request Forwarding
- [ ] Create a proxy handler that forwards the incoming request to the active model's `BackendUrl`:
  - Preserve the full request path (e.g., `/v1/chat/completions`)
  - Forward the request body unchanged
  - Forward all headers except hop-by-hop headers (`Host`, `Connection`, etc.)
  - Use `HttpClient` with a reasonable timeout (e.g., 5 minutes for streaming responses)
- [ ] Stream the response back to the client (do not buffer the entire response in memory)
- [ ] Preserve HTTP status codes from the backend

### 4.4 Idle Timeout Shutdown
- [ ] Create `Services/IdleTimeoutService.cs`:
  - Register as a `BackgroundService` (`IHostedService`)
  - Track `lastRequestTimestamp` — updated on every `/v1/*` request
  - On each tick (e.g., check every 30 seconds), compare elapsed time since last request to `IdleTimeoutSeconds`
  - If timeout exceeded, call `ModelManager.StopActiveModelAsync()` and log the shutdown
  - Reset the timer after shutdown

### 4.5 Last Request Tracking
- [ ] Create a simple `IRequestTracker` service (or integrate into existing middleware):
  - Thread-safe timestamp update (`Interlocked.Exchange` or `DateTimeOffset.UtcNow`)
  - Exposed to `IdleTimeoutService` via DI

## Notes
- Streaming responses (SSE) must be supported — use `HttpCompletionOption.ResponseHeadersRead`
- The idle timeout applies globally — all models are stopped after inactivity
- ngrok is out of scope — the router only exposes `localhost:9000`
