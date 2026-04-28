# Phase 3: Model Manager

## Goal
Implement the core model management system that tracks, starts, stops, and switches between local model backends.

**All tasks complete ✅**

## Tasks

### 3.1 ModelManager Core Class
- [x] Create `Services/ModelManager.cs`
- [x] Store `ActiveModelName` (nullable string)
- [x] Store `Process? ActiveProcess` reference
- [x] Use `lock` object to prevent parallel model switching

### 3.2 Start Model on Demand
- [x] Create `EnsureModelAsync(string modelName)` method:
  - Validates that the model name exists in config
  - If a model is already active, returns early
  - If a different model is active, stops it first (see 3.3)
  - Launch PowerShell script via `Process.Start()` with:
    - `FileName = "pwsh.exe"`
    - `Arguments = "-ExecutionPolicy Bypass -File <StartScript>"`
    - `RedirectStandardOutput` and `RedirectStandardError` for logging
    - `UseShellExecute = false`
  - Track the launched process in `ActiveProcess`

### 3.3 Stop Active Model
- [x] Create `StopActiveModelAsync()` method:
  - If no model is active, return early (no-op)
  - Gracefully signal the process to stop (`Process.Kill(false)` — SIGTERM equivalent on Windows)
  - Wait up to **5 seconds** for the process to exit
  - After 5 seconds, fallback to force kill (`Process.Kill(true)`)
  - Clear `ActiveModelName` and `ActiveProcess`

### 3.4 Health Check
- [x] Create `WaitForHealthCheckAsync(string backendUrl)` method:
  - Poll the backend's `/health` endpoint
  - Retry with a delay (500ms intervals) for up to 2 minutes
  - Return `true` if health check succeeds, `false` if it times out
  - Use `HttpClient` for polling

### 3.5 Error Handling
- [x] If model start fails (process doesn't launch), throw a descriptive exception
- [x] If health check fails within timeout, throw an exception (handled as 503 in routing layer)
- [x] Log process stdout/stderr for debugging model startup issues

### 3.6 Extensibility
- [x] Design `ModelManager` as a `Singleton` service registered in DI
- [x] Keep the interface clean for future parallel model support (e.g., route by conversation ID)
- [x] Add `IsActive` property and `ActiveModelName` getter

## Notes
- Idle timeout shutdown logic is covered in TODO_p4.md (separate concern)
- Model switching is serial — no parallel model loading
- PowerShell scripts must already exist on the user's Windows machine at the configured paths
