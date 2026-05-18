# ModelManager (IMetaModelManager)

## Responsibility

Core model lifecycle management: lazy start, hot switching, idle shutdown, health checks, and process cleanup. Orchestrates the full lifecycle of llama-server instances.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `IModelRepository` | Process state tracking (in-memory) |
| `DefaultModelLauncher` | PowerShell process launch + health polling |
| `HealthChecker` | `/health` endpoint polling |
| `GpuMemoryChecker` | VRAM availability check (deprecated, disabled) |
| `ProcessKiller` | Graceful shutdown with timeout fallback |
| `ILogger<ModelManager>` | Structured logging |

## Key Patterns

### Lazy Start Pattern

```csharp
public async Task EnsureModelAsync(string modelName, ModelLaunchParams? launchParams = null, CancellationToken ct = default)
{
    // Check 1: Is model already running AND healthy?
    var currentState = await _repository.GetStateAsync(modelName, ct);
    if (currentState is not null)
    {
        var backendHealthy = await _launcher.IsModelRunningV2(backendUrl, ct);
        if (backendHealthy) return; // Already running — exit early
    }

    // Check 2: Process tracked and alive?
    if (currentState is not null)
    {
        try
        {
            var process = Process.GetProcessById(currentState.ProcessId);
            if (!process.HasExited) return; // Still alive — exit early
        }
        catch (ArgumentException) { /* Process died */ }
    }

    // All checks failed — start model
    await SwitchModelAsync(modelName, modelConfig, launchParams, ct);
}
```

**Behavior:**
- Returns immediately if model is running and healthy
- Restarts if process died but state still tracked
- Full restart if no state exists

### Hot Switching Pattern

```csharp
private async Task SwitchModelAsync(string modelName, ModelConfig modelConfig, ...)
{
    // Stop current model if different AND target not exclusive
    var currentModelName = await GetActiveModelNameAsync(ct);
    if (currentModelName != modelName && !modelConfig.Exclusive)
    {
        await StopActiveModelAsync(ct);
    }

    await StartModelAsync(modelName, modelConfig, launchParams, ct);
}
```

**Behavior:**
- Stops current model before starting new one (unless target is `Exclusive`)
- Exclusive models block hot-switching entirely

### Health Check Pattern

```csharp
private async Task<bool> WaitForHealthCheckAsync(string backendUrl, CancellationToken ct)
{
    var healthPath = "/health";
    var url = $"{backendUrl.TrimEnd('/')}{healthPath}";
    
    var (success, reason) = await _healthChecker.PollAsync(
        url,
        TimeSpan.FromMinutes(_timeouts.HealthCheckPollTimeoutMinutes),
        TimeSpan.FromMilliseconds(_timeouts.HealthCheckPollDelayMs),
        ct);
    
    return success; // Throws if failed
}
```

**Configuration:**
- `HealthCheckPollTimeoutMinutes`: 5 minutes max wait
- `HealthCheckPollDelayMs`: 500ms between polls
- Fails fast with exception on timeout

### Process Cleanup Pattern

```csharp
public async Task ShutdownAsync(CancellationToken ct = default)
{
    await _launcher.ShutdownAllAsync(_logger, ct); // Kill all tracked processes
    await _repository.ClearStateAsync(ct);        // Clear state repository
}
```

**Trigger:** Registered on `IHostApplicationLifetime.ApplicationStopping` in `Program.cs`.

## State Management

### ModelProcessState Record

```csharp
public record ModelProcessState(
    string ModelName,
    int ProcessId,
    DateTimeOffset StartedAt);
```

**Lifecycle:**
1. Set when model starts (via `_launcher.StartAsync()`)
2. Cleared on stop or process death
3. Read by `GetActiveModelNameAsync()` and `IsIdleAsync()`

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Model not found in config | Throws `InvalidOperationException` with available models list |
| Health check timeout | Logs warning, stops model, throws `InvalidOperationException` |
| Process already exited | Catches `ArgumentException`, clears state, restarts model |
| VRAM insufficient | Throws `InvalidOperationException` (currently disabled) |

## Workflow: Adding a New Model

1. Add to `appsettings.json`:
   ```json
   "Models": {
     "new-model-name": { 
       "StartScript": "%NEW_MODEL_START_SCRIPT%",
       "BackendUrl": "http://localhost:8001"
     }
   }
   ```

2. Create PowerShell script in `powershell/` directory (see existing scripts)

3. Script must:
   - Use port 8001 (sequential execution)
   - Expose `/health` endpoint (llama-server default)
   - Support `-Temperature`, `-TopP`, `-PresencePenalty` launch params

4. Set environment variable for script path: `NEW_MODEL_START_SCRIPT="C:\scripts\new-model.ps1"`

## Workflow: Model Lifecycle Events

```
Request received 
  → EnsureModelAsync() called
  → Check if running + healthy (early exit if yes)
  → Check process alive (restart if died)
  → SwitchModelAsync()
    → Stop current model (if different + not exclusive)
    → StartModelAsync()
      → Launch PowerShell process
      → Capture stdout/stderr events
      → Wait for health check
      → Set state in repository
  → Forward request to backend
```

## Critical Notes

- **VRAM check disabled**: `CheckVramAvailability()` is present but not called. Keep as reference for future use.
- **Port sharing**: All models use port 8001 — sequential, not concurrent execution.
- **Process tracking**: Only PowerShell processes launched via `DefaultModelLauncher` are tracked. Manual launches won't be cleaned up on shutdown.
