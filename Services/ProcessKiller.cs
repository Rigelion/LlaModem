using System.Diagnostics;
using System.Management;
using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class ProcessKiller : IProcessKiller
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
        // Kill all descendant processes first
        var descendants = GetDescendantProcessIds(parentPid);
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
                logger.LogDebug(ex, "Could not kill child process PID: {Pid}", pid);
            }
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
            logger.LogDebug(ex, "Error killing main process PID: {Pid}", parentPid);
        }
    }

    /// <summary>
    /// Recursively finds all descendant process IDs for a given parent PID using WMI.
    /// </summary>
    private static HashSet<int> GetDescendantProcessIds(int parentPid)
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
                    var grandchildren = GetDescendantProcessIds(pid);
                    foreach (var gpId in grandchildren)
                    {
                        descendantIds.Add(gpId);
                    }
                }
            }
        }
        catch
        {
            // WMI may not be available on all systems — log and continue without tree kill
            // This is a best-effort operation
        }
#pragma warning restore CA1416 // Validate platform compatibility

        return descendantIds;
    }
}
