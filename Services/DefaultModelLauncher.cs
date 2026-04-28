using System.Diagnostics;

namespace LlaModem.Services;

public class DefaultModelLauncher : IModelLauncher
{
    public async Task<Process?> StartAsync(string modelName, string scriptPath)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        // Set window title to model name so we can identify the process later
        var escapedScript = scriptPath.Replace("'", "''");
        var arguments =
            "-ExecutionPolicy Bypass -Command \"$Host.UI.RawUI.WindowTitle = 'qwen-smart'; & 'F:/llama/llama-qwen36-SMART.ps1'\"";
        var ps = ResolvePowerShellExe();
        var psi = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExe(),
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        return Process.Start(psi);
    }
    
    
    static string ResolvePowerShellExe()
    {
        var pwsh = "C:\\Program Files\\PowerShell\\7\\pwsh.exe";

        if (File.Exists(pwsh))
            return pwsh;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"
        );
    }

    public bool IsModelRunning(string modelName)
    {
        // Check both pwsh and powershell processes since either could be used
        var psProcesses = Process.GetProcessesByName("pwsh")
            .Concat(Process.GetProcessesByName("powershell"));
        return psProcesses.Any(p => p.MainWindowTitle.Contains(modelName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task StopAsync(Process process, string modelName, ILogger logger)
    {
        logger.LogInformation("Stopping model '{Model}' (PID: {Pid})", modelName, process.Id);

        try
        {
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping model '{Model}'", modelName);
        }

        logger.LogInformation("Model '{Model}' stopped", modelName);
    }
}
