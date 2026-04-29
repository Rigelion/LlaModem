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
    Task<Process?> StartAsync(string modelName, string scriptPath, ModelLaunchParams? launchParams = null);

    /// <summary>
    /// Checks whether a PowerShell window with the given model name title is currently running.
    /// </summary>
    bool IsModelRunning(string modelName);

    /// <summary>
    /// Checks whether the backend URL responds to a health check, indicating the model's llama-server is running.
    /// </summary>
    Task<bool> IsModelRunningV2(string backendUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the given process gracefully, with force-kill fallback after 5 seconds.
    /// </summary>
    Task StopAsync(Process process, string modelName, ILogger logger);

    /// <summary>
    /// Shuts down all tracked PowerShell windows (used on application shutdown).
    /// </summary>
    Task ShutdownAllAsync(ILogger logger);
}
