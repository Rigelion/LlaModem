# Phase 3: Model Manager

## Goal
Implement the core model management system that tracks, starts, stops, and switches between local model backends.

## Tasks

### 3.1 ModelManager Core Class
- [ ] Create `Services/ModelManager.cs`
- [ ] Store `ActiveModelName` (nullable string)
- [ ] Store `Process? ActiveProcess` reference
- [ ] Use `SemaphoreSlim(1, 1)` to lock model switching and prevent parallel loading

### 3.2 Start Model on Demand
- [ ] Create `StartModelAsync(string modelName)` method:
  - Acquire the semaphore (with timeout to avoid indefinite blocking)
  - Validate that the model name exists in config
  - If a model is already active, stop it first (see 3.3)
  - Launch PowerShell script via `Process.Start()` with:
    - `FileName = "pwsh.exe"` (or `"powershell.exe"` for fallback)
    - `Arguments = "-ExecutionPolicy Bypass -File <StartScript>"`
    - `RedirectStandardOutput` and `RedirectStandardError` for logging
    - `UseShellExecute = false`
  - Track the launched process in `ActiveProcess`

### 3.3 Stop Active Model
- [ ] Create `StopActiveModelAsync()` method:
  - If no model is active, return early (no-op)
  - Gracefully signal the process to stop (`Process.Kill(false)` or send SIGTERM equivalent on Windows)
  - Wait up to **5 seconds** for the process to exit
  - After 5 seconds, fallback to force kill (`Process.Kill(true)`)
  - Clear `ActiveModelName` and `ActiveProcess`

### 3.4 Health Check
- [ ] Create `WaitForHealthCheckAsync(string backendUrl)` method:
  - Poll the backend's health endpoint (e.g., `/health` or `/ping` — use a configurable path)
  - Retry with a delay (e.g., 500ms intervals) for a configurable timeout (e.g., 60 seconds)
  - Return `true` if health check succeeds, `false` if it times out
  - Use `HttpClient` for polling

### 3.5 Error Handling
- [ ] If model start fails (process doesn't launch), throw a descriptive exception
- [ ] If health check fails within timeout, return `503 Service Unavailable` with error details
- [ ] Log process stdout/stderr for debugging model startup issues

### 3.6 Extensibility
- [ ] Design `ModelManager` as a `Singleton` service registered in DI
- [ ] Keep the interface clean for future parallel model support (e.g., route by conversation ID)
- [ ] Add `IsActive` property and `GetCurrentModelName()` getter

## Notes
- Idle timeout shutdown logic is covered in TODO_p4.md (separate concern)
- Model switching is serial — no parallel model loading
- PowerShell scripts must already exist on the user's Windows machine at the configured paths
