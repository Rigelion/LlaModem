using System.Diagnostics;
using System.Management;

namespace LlaModem.Services;

public class DefaultModelLauncher : IModelLauncher
{
    /// <summary>
    /// Always uses Windows PowerShell (powershell.exe) from the system directory.
    /// </summary>
    private const string PowerShellExe = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    public async Task<Process?> StartAsync(string modelName, string scriptPath)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        // Set window title to model name so we can identify the process later
        var escapedScript = scriptPath.Replace("'", "''");
        var arguments =
            "-ExecutionPolicy Bypass -Command \"$Host.UI.RawUI.WindowTitle = 'qwen-smart'; & 'F:/llama/llama-qwen36-SMART.ps1'\"";
        var psi = new ProcessStartInfo
        {
            FileName = PowerShellExe,
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        return Process.Start(psi);
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
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
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

        logger.LogInformation("Model '{Model}' stopped", modelName);
    }

    /// <summary>
    /// Recursively finds all descendant process IDs for a given parent PID using WMI.
    /// </summary>
    private static HashSet<int> GetDescendantProcessIds(int parentPid)
    {
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

        return descendantIds;
    }
}
