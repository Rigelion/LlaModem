# DefaultModelLauncher

**Entity Type:** Service  
**Responsibility:** Launches `llama-server` processes via PowerShell scripts. Handles process lifecycle, health polling, and graceful shutdown of all tracked models.

## Dependencies

- `ILogger<DefaultModelLauncher>` - Process event logging
- `IOptions<RouterConfig>` - Timeout configuration (health check, shutdown)

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
    process.OutputDataReceived += (s, e) => _logger.LogDebug("[{Model}] {Data}", modelName, e.Data);
    process.ErrorDataReceived += (s, e) => _logger.LogDebug("[{Model} ERR] {Data}", modelName, e.Data);
    
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

Polls `/health` endpoint to check if backend is responsive before assuming model is running.

### ShutdownAllAsync

Called on `IHostApplicationLifetime.ApplicationStopping` in `Program.cs`. Iterates all tracked processes and stops them gracefully with 5-second timeout, then force kill fallback.

## Process Lifecycle

### Start → Health Check → Running

1. **Launch:** PowerShell process started via `StartAsync()`
2. **Output capture:** stdout/stderr events logged at debug level
3. **Health polling:** `ModelManager.WaitForHealthCheckAsync()` polls `/health` endpoint (5 min max, 500ms interval)
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

## Critical Notes

- **PowerShell only:** Scripts must be PowerShell (.ps1), not batch or CMD
- **Window hidden:** `CreateNoWindow = true` prevents popup windows
- **Output buffering:** `BeginOutputReadLine()` enables real-time logging
- **Process cleanup:** Only processes launched via this launcher tracked on shutdown