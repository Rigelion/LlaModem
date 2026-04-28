# Phase 5: Admin Endpoints, Logging & Polish

## Goal
Add admin endpoints for model management, structured logging, and final polish for production readiness.

**All tasks complete ✅**

## Tasks

### 5.1 Admin Endpoint — Switch Model
- [x] Create `POST /admin/model` endpoint:
  - Accept JSON body: `{ "model": "qwen-smart" }`
  - Trigger model switch via `ModelManager.EnsureModelAsync(modelName)`
  - Return `200 OK` with the new active model name on success
  - Return `400 Bad Request` if model name is invalid or missing
  - Return `503 Service Unavailable` with details if start fails

### 5.2 Admin Endpoint — Status
- [x] Create `GET /admin/status` endpoint:
  - Return JSON: `{ "activeModel": "qwen-smart", "backendUrl": "http://localhost:8001", "uptime": "..." }`
  - Return `{"activeModel": null}` if no model is running

### 5.3 Admin Endpoint — Force Stop
- [x] Create `POST /admin/stop` endpoint:
  - Call `ModelManager.StopActiveModelAsync()`
  - Return `200 OK` with confirmation or `400` if nothing is active

### 5.4 Logging
- [x] Configure structured logging (use `Microsoft.Extensions.Logging`):
  - Log model start/stop events (ModelManager)
  - Log health check results (success/failure) (ModelManager)
  - Log request forwarding (method, path, model, status code) (Program.cs proxy)
  - Never log full Authorization header values (BasicAuthMiddleware)
  - Use `ILogger<T>` per service class

### 5.5 Health Check for the Router Itself
- [x] Create `GET /health` endpoint (unauthenticated):
  - Return `200 OK` with simple JSON: `{ "status": "ok" }`
  - Include active model info

### 5.6 Program.cs Final Wiring
- [x] Register all services in DI:
  - `IConfiguration` (done in Phase 1)
  - `ModelManager` as Singleton
  - `IdleTimeoutService` as HostedService
  - `RequestTracker` as Singleton
- [x] Apply auth middleware to `/v1/*` routes
- [x] Map admin endpoints (`/admin/*`) and health endpoint (`/health`)
- [x] Configure Kestrel to listen on the configured `ListenUrl`

## Notes
- Admin endpoints are separate from the proxy routes for security clarity
- The router's own `/health` stays unauthenticated for external monitoring
- Logging should be console-based (no file logging needed unless requested)
