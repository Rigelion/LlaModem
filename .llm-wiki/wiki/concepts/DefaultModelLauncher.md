---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-002]]
status: complete
---

# DefaultModelLauncher

**Description:** Launches `llama-server` processes via PowerShell scripts. Handles process lifecycle, health polling, and graceful shutdown of all tracked models.

## Process Management Pattern

### StartAsync
```csharp
public async Task<Process> StartAsync(
    string modelName, 
    string startScript, 
    ModelLaunchParams? launchParams,
    CancellationToken ct = default)
{
    // Build command line with PowerShell execution policy bypass
    var args = $"-ExecutionPolicy Bypass -File {startScript}";
    if (launchParams is not null)
    {
        args += $" -Temperature {launchParams.Temperature}";
        args += $" -TopP {launchParams.TopP}";
        args += $" -PresencePenalty {launchParams.PresencePenalty}";
    }

    // Start hidden PowerShell process
    var startInfo = new ProcessStartInfo("powershell.exe", args)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true  // No popup windows
    };

    var process = Process.Start(startInfo);
    
    // Capture output events for structured logging
    process.OutputDataReceived += (s, e) => _logger.LogDebug("[{Model}] {Data}", modelName, e.Data);
    process.ErrorDataReceived += (s, e) => _logger.LogDebug("[{Model} ERR] {Data}", modelName, e.Data);
    
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    return process;
}
```

**Behavior:**
- PowerShell with `-ExecutionPolicy Bypass` flag prevents policy errors
- `CreateNoWindow = true` prevents popup windows on Windows
- Output/stderr captured via events for debug-level logging
- Returns `Process` object for tracking and cleanup

### StopAsync
```csharp
public async Task StopAsync(int processId, CancellationToken ct = default)
{
    var process = Process.GetProcessById(processId);
    
    // Graceful shutdown with 5-second timeout
    if (await process.WaitForExitAsync(5000, ct))
        return;

    // Force kill fallback
    process.Kill();
}
```

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

## Related Entities

- [[entities/DefaultModelLauncher]] - PowerShell process launcher
- [[entities/ModelManager]] - Model lifecycle orchestration
- [[entities/HealthChecker]] - Backend health check polling
- [[entities/ConfigRecords]] - Configuration records

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
