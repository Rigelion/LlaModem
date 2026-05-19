---
type: entity
created: 2026-05-18
updated: 2026-05-19
status: complete
---

# IdleTimeoutService

**Entity Type:** Service  
**Responsibility:** Monitors model inactivity and triggers idle shutdown. Runs as a background task that checks elapsed time since last request.

## Dependencies

- `IModelManager` - Stop active model when idle timeout reached
- `IMetaModelRepository` - Track last request timestamp
- `ILogger<IdleTimeoutService>` - Idle timeout event logging
- `IOptions<RouterConfig>` - Idle timeout configuration

## Key Methods

### StartMonitoringAsync

```csharp
public async Task StartMonitoringAsync(CancellationToken ct = default)
{
    while (!ct.IsCancellationRequested)
    {
        await Task.Delay(_config.IdleTimeoutSeconds * 1000, ct);
        
        var lastRequestTime = await _repository.GetLastRequestTimeAsync();
        
        if (lastRequestTime is null)
            continue;

        var elapsed = DateTimeOffset.UtcNow - lastRequestTime.Value;
        
        if (elapsed.TotalSeconds >= _config.IdleTimeoutSeconds)
        {
            _logger.LogInformation("Idle timeout reached ({Elapsed}s), stopping active model", elapsed.TotalSeconds);
            
            try
            {
                await _modelManager.StopActiveModelAsync();
                _logger.LogInformation("Idle shutdown complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop model on idle timeout");
            }
        }
    }
}
```

**Behavior:**
- Polls every `IdleTimeoutSeconds` seconds (default: 600s = 10 minutes)
- Checks last request timestamp from repository
- Triggers shutdown if elapsed time exceeds threshold
- Continues monitoring after shutdown (ready for next model)

### StopMonitoringAsync

```csharp
public async Task StopMonitoringAsync()
{
    // Called on app shutdown to cancel background task
}
```

## Configuration

Reads from `RouterConfig`:
- `IdleTimeoutSeconds` - Seconds of inactivity before shutdown (default: 600)
- `EnableIdleShutdown` - Enable/disable idle monitoring (default: true)

## Usage Pattern

```csharp
// In Program.cs
var idleService = new IdleTimeoutService(
    modelManager, 
    repository, 
    config);

await idleService.StartMonitoringAsync(ct);

// On shutdown
await idleService.StopMonitoringAsync();
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Model not found | Logs warning, continues monitoring |
| Shutdown throws exception | Logs error, continues monitoring |
| Repository unavailable | Logs error, skips check for this cycle |

## Related Entities

- [[entities/ModelManager]] - Performs actual shutdown via `StopActiveModelAsync()`
- [[entities/ConfigRecords]] - Configures idle timeout threshold
- [[entities/DefaultModelLauncher]] - Terminates process on shutdown

## Related Concepts

- [[entities/IdleTimeoutService]] - Idle timeout monitoring service