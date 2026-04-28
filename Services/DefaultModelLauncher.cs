using System.Diagnostics;

namespace LlaModem.Services;

public class DefaultModelLauncher : IModelLauncher
{
    public async Task<Process?> StartAsync(string scriptPath)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        return Process.Start(psi);
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
