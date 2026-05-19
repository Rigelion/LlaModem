---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-002]]
status: complete
---

# ModelManager

**Description:** Orchestrates model lifecycle operations: lazy start, hot switching, idle shutdown, and health checks. Central coordinator for model state management.

## Key Methods

### EnsureModelAsync
```csharp
public async Task<bool> EnsureModelAsync(
    string modelName, 
    ModelLaunchParams? launchParams = null,
    CancellationToken ct = default)
{
    // Check if model already running
    var activeModel = await GetActiveModelNameAsync();
    if (activeModel == modelName)
        return true;

    // Stop current model if different
    if (activeModel is not null)
        await StopActiveModelAsync();

    // Start new model
    var config = _config.Models[modelName];
    var process = await _launcher.StartAsync(
        modelName, 
        config.StartScript, 
        launchParams, 
        ct);

    // Wait for health check
    await WaitForHealthCheckAsync(modelName, process, ct);

    // Update state repository
    await _repository.SetStateAsync(new ModelProcessState {
        ModelName = modelName,
        ProcessId = process.Id,
        StartedAt = DateTimeOffset.UtcNow
    });

    return true;
}
```
**Behavior:** Lazy start (only launches when first requested), hot switching (stops current model before starting new one), health check polling (5 min max, 500ms interval).

### StopActiveModelAsync
```csharp
public async Task StopActiveModelAsync()
{
    var state = await _repository.GetStateAsync();
    if (state is null) return;

    // Stop process via launcher
    await _launcher.StopProcessAsync(state.ProcessId);

    // Clear repository state
    await _repository.ClearStateAsync();
}
```
**Behavior:** Graceful shutdown with 5-second timeout, force kill fallback if not stopped in time, clears state repository on success.

## State Management
**Repository Pattern:** `IModelRepository` stores `ModelProcessState`:
```csharp
public sealed record ModelProcessState(
    string? ModelName,
    int? ProcessId,
    DateTimeOffset? StartedAt);
```
Thread-safe dictionary keyed by model name.

## Related Entities

- [[entities/ModelManager]] - Lifecycle orchestration service
- [[entities/DefaultModelLauncher]] - PowerShell process launcher
- [[entities/HealthChecker]] - Backend health check polling
- [[entities/IdleTimeoutService]] - Idle timeout monitoring service
- [[entities/ConfigRecords]] - Configuration records

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
