using System.Diagnostics;
using System.Management;

namespace LlaModem.Services;

public class DefaultModelLauncher : IModelLauncher
{
    /// <summary>
    /// Always uses Windows PowerShell (powershell.exe) from the system directory.
    /// </summary>
    private const string PowerShellExe = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    /// <summary>
    /// Thread-safe collection of all PowerShell processes tracked by LlaModem.
    /// </summary>
    private readonly HashSet<Process> _trackedProcesses = new();
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Lock for protecting the tracked processes collection.
    /// </summary>
    private readonly object _lock = new();

    public DefaultModelLauncher(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Process?> StartAsync(string modelName, string scriptPath, ModelLaunchParams? launchParams = null)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        // Set window title to the model name so we can identify the process later
        var escapedScript = scriptPath.Replace("'", "''");

        // Build optional launch params suffix
        var paramParts = new List<string>();
        if (launchParams?.Temperature.HasValue == true) paramParts.Add($"-Temperature {launchParams.Temperature}");
        if (launchParams?.TopP.HasValue == true) paramParts.Add($"-TopP {launchParams.TopP}");
        if (launchParams?.PresencePenalty.HasValue == true) paramParts.Add($"-PresencePenalty {launchParams.PresencePenalty}");
        var paramSuffix = paramParts.Count > 0 ? " " + string.Join(" ", paramParts) : string.Empty;

        var arguments =
            $"-ExecutionPolicy Bypass -Command \"$Host.UI.RawUI.WindowTitle = '{modelName}'; & '{escapedScript}'{paramSuffix}\"";
        var psi = new ProcessStartInfo
        {
            FileName = PowerShellExe,
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);

        // Track the process for shutdown cleanup
        if (process is not null)
        {
            lock (_lock)
            {
                _trackedProcesses.Add(process);
            }
        }

        return process;
    }

    public bool IsModelRunning(string modelName)
    {
        // Only check powershell.exe processes (never pwsh)
        var psProcesses = Process.GetProcessesByName("powershell");
        return psProcesses.Any(p => p.MainWindowTitle.Contains(modelName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsModelRunningV2(string backendUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthUrl = $"{backendUrl.TrimEnd('/')}/health";
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync(healthUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task StopAsync(Process process, string modelName, ILogger logger)
    {
        logger.LogInformation("Stopping model '{Model}' (PID: {Pid})", modelName, process.Id);

        try
        {
            if (!process.HasExited)
            {
                // Kill all descendant processes first (tree kill)
                var descendants = GetDescendantProcessIds(process.Id);
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

                // Now kill the main PowerShell process
                if (!process.HasExited)
                {
                    process.Kill(false);

                    var exited = process.WaitForExit(5000);
                    if (!exited)
                    {
                        logger.LogWarning("Model '{Model}' did not exit gracefully within 5s, force killing", modelName);
                        process.Kill(true);
                        process.WaitForExit();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping model '{Model}'", modelName);
        }

        // Remove from tracked processes
        lock (_lock)
        {
            _trackedProcesses.Remove(process);
        }

        logger.LogInformation("Model '{Model}' stopped", modelName);
    }

    /// <summary>
    /// Shuts down all tracked PowerShell windows on application shutdown.
    /// Kills each process gracefully with a 5-second timeout, then force-kills if needed.
    /// </summary>
    public async Task ShutdownAllAsync(ILogger logger)
    {
        Process[] processesToKill;

        lock (_lock)
        {
            // Snapshot the current set of tracked processes
            processesToKill = _trackedProcesses.ToArray();
            _trackedProcesses.Clear();
        }

        if (processesToKill.Length == 0)
        {
            logger.LogInformation("No tracked PowerShell windows to shut down");
            return;
        }

        logger.LogInformation("Shutting down {Count} tracked PowerShell window(s)", processesToKill.Length);

        // Kill all descendant processes first (tree kill) for each process
        foreach (var process in processesToKill)
        {
            try
            {
                if (process.HasExited)
                    continue;

                var descendants = GetDescendantProcessIds(process.Id);
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
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error getting descendants for PID: {Pid}", process.Id);
            }
        }

        // Now kill the main PowerShell processes
        foreach (var process in processesToKill)
        {
            try
            {
                if (process.HasExited)
                    continue;

                logger.LogInformation("Shutting down PowerShell window PID: {Pid}", process.Id);
                process.Kill(false);

                var exited = process.WaitForExit(5000);
                if (!exited)
                {
                    logger.LogWarning("PowerShell window (PID: {Pid}) did not exit gracefully within 5s, force killing",
                        process.Id);
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error shutting down PowerShell window PID: {Pid}", process.Id);
            }
        }

        logger.LogInformation("All tracked PowerShell windows shut down");
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
