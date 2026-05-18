# ModelLifecycleManagement

**Concept Type:** Architecture Pattern  
**Description:** Automatic model lifecycle management with lazy start, hot switching, idle shutdown, and health checks.

## Key Phases

### 1. Lazy Start
Models only launch when first requested. No pre-loading or warm-up. Reduces resource usage when models not in use.

### 2. Hot Switching
Request a different model → current stops, new starts. Seamless transition with automatic cleanup of old process and startup of new one.

### 3. Idle Shutdown
Stops active model after `IdleTimeoutSeconds` (default: 600s) of inactivity. Prevents GPU memory waste during idle periods.

### 4. Health Checks
Poll llama-server `/health` endpoint until healthy or timeout (5 min max, 500ms interval). Ensures backend is responsive before routing requests.

### 5. Process Cleanup
On app shutdown, all tracked PowerShell processes terminated gracefully with 5-second timeout, then force kill fallback.

## State Management

**Repository Pattern:** `IModelRepository` stores `ModelProcessState` (modelName, processId, startedAt). Thread-safe dictionary keyed by model name.

**State Transitions:**
```
Stopped → Starting → Running → Stopped
         ↓
      Failed (health check timeout)
```

## Implementation Components

- **ModelManager**: Orchestrates lifecycle (lazy start, hot switch, idle shutdown)
- **DefaultModelLauncher**: Launches PowerShell processes, captures output events
- **HealthChecker**: Polls `/health` endpoint with configurable timeout/poll interval
- **IdleTimeoutService**: Background service monitors inactivity, triggers shutdown

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Script path invalid | Process start fails — logs error, returns null |
| PowerShell execution blocked | `-ExecutionPolicy Bypass` flag prevents policy errors |
| Port already in use | llama-server exits with error — captured in stderr log |
| Health check timeout | ModelManager throws `InvalidOperationException` |

## Critical Notes

- **Sequential execution:** All models share port 8001 (not concurrent)
- **PowerShell only:** Scripts must be `.ps1`, not batch or CMD
- **Window hidden:** `CreateNoWindow = true` prevents popup windows
- **Output capture:** Real-time logging via stdout/stderr events

## Related Entities

- [[DefaultModelLauncher]]
- [[ModelManager]] (not documented yet)
- [[HealthChecker]] (not documented yet)
- [[IdleTimeoutService]] (not documented yet)