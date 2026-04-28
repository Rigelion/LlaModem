using System.Diagnostics;

namespace LlaModem.Services;

/// <summary>
/// Abstracts model process launching for testability.
/// </summary>
public interface IModelLauncher
{
    /// <summary>
    /// Starts the model via its start script. Returns the process, or null if launch failed.
    /// </summary>
    Task<Process?> StartAsync(string modelName, string scriptPath);

    /// <summary>
    /// Checks whether a PowerShell window with the given model name title is currently running.
    /// </summary>
    bool IsModelRunning(string modelName);

    /// <summary>
    /// Stops the given process gracefully, with force-kill fallback after 5 seconds.
    /// </summary>
    Task StopAsync(Process process, string modelName, ILogger logger);
}
