using System.Diagnostics;
using System.Management;
using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class ProcessKiller
{
    private readonly int _gracefulShutdownTimeoutMs;

    public ProcessKiller(IOptions<RouterConfig> config)
    {
        _gracefulShutdownTimeoutMs = config.Value.Timeouts.GracefulShutdownTimeoutSeconds * 1000;
    }

    public async Task StopAsync(Process process, string modelName, ILogger logger)
    {
        logger.LogInformation("Stopping model '{Model}' (PID: {Pid})", modelName, process.Id);

        try
        {
            if (!process.HasExited)
            {
                await KillProcessTreeAsync(process.Id, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping model '{Model}'", modelName);
        }

        logger.LogInformation("Model '{Model}' stopped", modelName);
    }

    public async Task ShutdownAllAsync(IEnumerable<Process> processes, ILogger logger)
    {
        var processList = processes as Process[] ?? processes.ToArray();

        if (processList.Length == 0)
        {
            logger.LogInformation("No tracked PowerShell windows to shut down");
            return;
        }

        logger.LogInformation("Shutting down {Count} tracked PowerShell window(s)", processList.Length);

        foreach (var process in processList)
        {
            try
            {
                if (process.HasExited)
                    continue;

                logger.LogInformation("Shutting down PowerShell window PID: {Pid}", process.Id);
                await KillProcessTreeAsync(process.Id, logger);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error shutting down PowerShell window PID: {Pid}", process.Id);
            }
        }

        logger.LogInformation("All tracked PowerShell windows shut down");
    }

    private async Task KillProcessTreeAsync(int parentPid, ILogger logger)
    {
        // Get all descendant process IDs
        var descendants = GetDescendantProcessIds(parentPid, logger);
        
        if (descendants.Count == 0)
        {
            logger.LogWarning("No descendant processes found for PID {Pid}. Using fallback kill strategy.", parentPid);
        }
        else
        {
            logger.LogInformation("Found {Count} descendant process(es) for PID {Pid}", descendants.Count, parentPid);
        }

        // Kill all descendant processes first
        foreach (var pid in descendants)
        {
            try
            {
                var descendantProcess = Process.GetProcessById(pid);
                if (!descendantProcess.HasExited)
                {
                    logger.LogDebug("Killing child process PID: {Pid}", pid);
                    descendantProcess.Kill(false);
                    descendantProcess.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not kill child process PID: {Pid}", pid);
            }
        }

        // Fallback: If no descendants were found, try to find and kill children manually
        if (descendants.Count == 0)
        {
            await KillChildrenManuallyAsync(parentPid, logger);
        }

        // Now kill the main process with graceful timeout
        try
        {
            var mainProcess = Process.GetProcessById(parentPid);
            if (!mainProcess.HasExited)
            {
                mainProcess.Kill(false);
                var exited = mainProcess.WaitForExit(_gracefulShutdownTimeoutMs);
                if (!exited)
                {
                    logger.LogWarning("Process (PID: {Pid}) did not exit gracefully within {Timeout}ms, force killing",
                        parentPid, _gracefulShutdownTimeoutMs);
                    mainProcess.Kill(true);
                    mainProcess.WaitForExit();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error killing main process PID: {Pid}", parentPid);
        }
    }

    /// <summary>
    /// Fallback method to find and kill child processes when WMI query fails or returns empty.
    /// Uses a broader search by querying all processes and filtering by parent PID.
    /// </summary>
    private async Task KillChildrenManuallyAsync(int parentPid, ILogger logger)
    {
        try
        {
            // Query all processes and find children of the parent
            var query = "SELECT * FROM Win32_Process";
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();

            var childPids = new List<int>();
            foreach (var obj in results)
            {
                try
                {
                    var processId = Convert.ToInt32(obj["ProcessId"]);
                    var parentId = Convert.ToInt32(obj["ParentProcessId"]);
                    
                    if (parentId == parentPid)
                    {
                        childPids.Add(processId);
                        logger.LogDebug("Found child process PID: {Pid} (parent: {ParentPid})", processId, parentPid);
                    }
                }
                catch
                {
                    // Skip malformed entries
                }
            }

            // Kill all found children with aggressive timeout
            foreach (var pid in childPids)
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        logger.LogDebug("Killing manually found child process PID: {Pid}", pid);
                        process.Kill(false);
                        // Use shorter timeout for children since they should exit quickly
                        if (!process.WaitForExit(2000))
                        {
                            logger.LogWarning("Child process PID: {Pid} did not exit, force killing", pid);
                            process.Kill(true);
                            process.WaitForExit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not kill manually found child process PID: {Pid}", pid);
                }
            }

            if (childPids.Count == 0)
            {
                logger.LogWarning("No child processes found via fallback for PID {Pid}. VRAM may not be released.", parentPid);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual child process search failed for PID {Pid}. VRAM may not be released.", parentPid);
        }
    }

    /// <summary>
    /// Recursively finds all descendant process IDs for a given parent PID using WMI.
    /// </summary>
    private static HashSet<int> GetDescendantProcessIds(int parentPid, ILogger? logger = null)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        var descendantIds = new HashSet<int>();
        try
        {
            var query = $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}";
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();

            foreach (var obj in results)
            {
                var pid = Convert.ToInt32(obj["ProcessId"]);
                if (!descendantIds.Contains(pid))
                {
                    descendantIds.Add(pid);
                    // Recursively find grandchildren
                    var grandchildren = GetDescendantProcessIds(pid, logger);
                    foreach (var gpId in grandchildren)
                    {
                        descendantIds.Add(gpId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "WMI query failed for parent PID {Pid}", parentPid);
            // Return empty set - fallback will be used
        }
#pragma warning restore CA1416 // Validate platform compatibility

        return descendantIds;
    }
}
