---
type: entity
created: 2026-05-18
updated: 2026-05-19
status: complete
---

# HealthChecker

**Entity Type:** Service  
**Responsibility:** Polls llama-server `/health` endpoint to verify backend readiness. Used by `ModelManager` during model startup and hot switching.

## Dependencies

- `HttpClient` - HTTP client for health polling
- `ILogger<HealthChecker>` - Health check event logging
- `IOptions<RouterConfig>` - Timeout configuration (poll timeout, poll interval)

## Key Methods

### PollAsync

```csharp
public async Task<bool> PollAsync(
    string backendUrl, 
    int maxAttempts = 60, 
    TimeSpan? pollInterval = null,
    CancellationToken ct = default)
{
    var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    
    for (int i = 0; i < maxAttempts; i++)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{backendUrl}/health", ct);
            
            if (response.IsSuccessStatusCode)
                return true;
            
            await Task.Delay(interval, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Health check attempt {Attempt} failed", i + 1);
            await Task.Delay(interval, ct);
        }
    }

    return false;
}
```

**Behavior:**
- Polls `/health` endpoint every 500ms (default)
- Max 60 attempts (30 seconds total timeout)
- Returns `true` on first successful response
- Returns `false` after max attempts exceeded

### WaitForHealthAsync

```csharp
public async Task WaitForHealthAsync(
    string backendUrl, 
    int timeoutSeconds = 300,
    CancellationToken ct = default)
{
    var success = await PollAsync(backendUrl, ct: ct);
    
    if (!success)
        throw new InvalidOperationException($"Backend at {backendUrl} failed health check");
}
```

**Usage:** Called by `ModelManager.EnsureModelAsync()` after process launch.

## Configuration

Reads from `RouterConfig`:
- `HealthCheckPollInterval` - Time between attempts (default: 500ms)
- `HealthCheckTimeoutSeconds` - Max wait time (default: 300s)

## Error Handling

| Scenario | Behavior |
|----------|----------|
| HTTP 4xx/5xx response | Logs warning, retries until timeout |
| Connection refused | Logs debug, retries until timeout |
| Timeout exceeded | Throws `InvalidOperationException` |
| CancellationToken cancelled | Throws `OperationCanceledException` |

## Related Entities

- [[entities/ModelManager]] - Orchestrates health check during startup
- [[entities/DefaultModelLauncher]] - Launches process that exposes `/health` endpoint
- [[entities/ConfigRecords]] - Configures poll interval and timeout

## Related Concepts

- [[entities/HealthChecker]] - Backend health check polling