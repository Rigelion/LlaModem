using System.Diagnostics;

namespace LlaModem.Services;

public class DefaultModelLauncher : IModelLauncher
{
    public async Task<Process?> StartAsync(string modelName, string scriptPath)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        // Set window title to model name so we can identify the process later
        var escapedScript = scriptPath.Replace("'", "''");
        var arguments = $"-ExecutionPolicy Bypass -Command \"$$Host.UI.RawUI.WindowTitle = '{modelName}'; & '{escapedScript}'\"";

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = arguments,
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        return Process.Start(psi);
    }

    public bool IsModelRunning(string modelName)
    {
        var pwshProcesses = Process.GetProcessesByName("pwsh");
        return pwshProcesses.Any(p => p.MainWindowTitle.Contains(modelName, StringComparison.OrdinalIgnoreCase));
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
