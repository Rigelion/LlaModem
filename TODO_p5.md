# Phase 5: Admin Endpoints, Logging & Polish

## Goal
Add admin endpoints for model management, structured logging, and final polish for production readiness.

## Tasks

### 5.1 Admin Endpoint — Switch Model
- [ ] Create `POST /admin/model` endpoint (no auth required initially, or reuse Basic Auth):
  - Accept JSON body: `{ "model": "qwen-smart" }`
  - Trigger model switch via `ModelManager.StartModelAsync(modelName)`
  - Return `200 OK` with the new active model name on success
  - Return `400 Bad Request` if model name is invalid or missing
  - Return `503 Service Unavailable` with details if start fails

### 5.2 Admin Endpoint — Status
- [ ] Create `GET /admin/status` endpoint:
  - Return JSON: `{ "activeModel": "qwen-smart", "backendUrl": "http://localhost:8001", "uptime": "..." }`
  - Return `{"activeModel": null}` if no model is running

### 5.3 Admin Endpoint — Force Stop
- [ ] Create `POST /admin/stop` endpoint:
  - Call `ModelManager.StopActiveModelAsync()`
  - Return `200 OK` with confirmation or `400` if nothing is active

### 5.4 Logging
- [ ] Configure structured logging (use `Microsoft.Extensions.Logging`):
  - Log model start/stop events
  - Log health check results (success/failure)
  - Log request forwarding (method, path, model, response time, status code)
  - Never log full Authorization header values
  - Use `ILogger<T>` per service class

### 5.5 Health Check for the Router Itself
- [ ] Create `GET /health` endpoint (unauthenticated):
  - Return `200 OK` with simple JSON: `{ "status": "ok" }`
  - Optionally include active model info

### 5.6 Program.cs Final Wiring
- [ ] Register all services in DI:
  - `IConfiguration` (already done)
  - `ModelManager` as Singleton
  - `IdleTimeoutService` as HostedService
  - `RequestTracker` as Singleton
- [ ] Apply auth middleware to `/v1/*` routes
- [ ] Map admin endpoints (`/admin/*`) and health endpoint (`/health`)
- [ ] Configure Kestrel to listen on the configured `ListenUrl`

## Notes
- Admin endpoints are separate from the proxy routes for security clarity
- The router's own `/health` stays unauthenticated for external monitoring
- Logging should be console-based (no file logging needed unless requested)
