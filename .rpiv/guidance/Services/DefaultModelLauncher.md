# DefaultModelLauncher

## Responsibility

Launches `llama-server` processes via PowerShell scripts. Handles process lifecycle, health polling, and graceful shutdown of all tracked models.

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ILogger<DefaultModelLauncher>` | Process event logging |
| `IOptions<RouterConfig>` | Timeout configuration (health check, shutdown) |

## Key Methods

### StartAsync

```csharp
public async Task<Process> StartAsync(
    string modelName, 
    string startScript, 
    ModelLaunchParams? launchParams,
    CancellationToken ct = default)
{
    // Build command line
    var args = $"-ExecutionPolicy Bypass -File {startScript}";
    if (launchParams is not null)
    {
        args += $" -Temperature {launchParams.Temperature}";
        args += $" -TopP {launchParams.TopP}";
        args += $" -PresencePenalty {launchParams.PresencePenalty}";
    }

    // Start process
    var startInfo = new ProcessStartInfo("powershell.exe", args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    var process = Process.Start(startInfo);
    
    // Capture output events
    process.OutputDataReceived += (s, e) => 
        _logger.LogDebug("[{Model}] {Data}", modelName, e.Data);
    process.ErrorDataReceived += (s, e) => 
        _logger.LogDebug("[{Model} ERR] {Data}", modelName, e.Data);
    
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    return process;
}
```

**Behavior:**
- Launches PowerShell with `-ExecutionPolicy Bypass` flag
- Passes launch params as script arguments (Temperature, TopP, PresencePenalty)
- Captures stdout/stderr for structured logging
- Returns `Process` object for tracking

### IsModelRunningV2

```csharp
public async Task<bool> IsModelRunningV2(string backendUrl, CancellationToken ct)
{
    try
    {
        var client = _httpClientFactory.CreateClient("ModelManager");
        var response = await client.GetAsync("/health", ct);
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

**Usage:** Called by `ModelManager` to check if backend is responsive before assuming model is running.

### ShutdownAllAsync

```csharp
public async Task ShutdownAllAsync(ILogger logger, CancellationToken ct)
{
    var states = await _repository.GetAllStatesAsync(ct);
    
    foreach (var state in states)
    {
        try
        {
            var process = Process.GetProcessById(state.ProcessId);
            await StopAsync(process, state.ModelName, logger);
        }
        catch (ArgumentException)
        {
            // Process already exited — skip
        }
    }
}
```

**Trigger:** Called on `IHostApplicationLifetime.ApplicationStopping` in `Program.cs`.

## Process Lifecycle

### Start → Health Check → Running

1. **Launch:** PowerShell process started via `StartAsync()`
2. **Output capture:** stdout/stderr events logged at debug level
3. **Health polling:** `ModelManager.WaitForHealthCheckAsync()` polls `/health` endpoint
4. **State set:** On health check success, `IModelRepository.SetStateAsync()` records process ID

### Stop → Cleanup → Idle

1. **Stop request:** `ModelManager.StopActiveModelAsync()` called
2. **Process termination:** `ProcessKiller.StopAsync()` sends SIGTERM with 5s timeout
3. **Force kill fallback:** If not stopped in 5s, sends SIGKILL
4. **State cleared:** `IModelRepository.ClearStateAsync()` removes process tracking

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Script path invalid | Process start fails — logs error, returns null |
| PowerShell execution blocked | `-ExecutionPolicy Bypass` flag prevents policy errors |
| Port already in use | llama-server exits with error — captured in stderr log |
| Health check timeout | ModelManager throws `InvalidOperationException` |

## Workflow: Model Startup Sequence

```
ModelManager.EnsureModelAsync() called
  → DefaultModelLauncher.StartAsync()
    → Launch PowerShell process
    → Capture stdout/stderr events
    → Return Process object
  → ModelManager.WaitForHealthCheckAsync()
    → Poll /health endpoint (5 min max, 500ms interval)
    → Success or throw exception
  → Set state in IModelRepository
  → Model ready to receive requests
```

## Testing Notes

- **Process mocking:** Hard to unit test — relies on real process creation
- **Integration testing:** Test via `ModelManager` end-to-end (mock repository)
- **Output capture:** Verify logging events in tests with `ILogger` mock

See `.rpiv/guidance/LlaModem.Tests/InMemoryModelRepositoryTests.cs` for state tracking tests.

## Critical Notes

- **PowerShell only:** Scripts must be PowerShell (.ps1), not batch or CMD
- **Window hidden:** `CreateNoWindow = true` prevents popup windows
- **Output buffering:** `BeginOutputReadLine()` enables real-time logging
- **Process cleanup:** Only processes launched via this launcher tracked on shutdown
