using System.Diagnostics;

namespace LlaModem.Services;

/// <summary>
/// Handles graceful process tree termination with force-kill fallback.
/// </summary>
public interface IProcessKiller
{
    /// <summary>
    /// Stops a single process tree gracefully (5s timeout), then force-kills if needed.
    /// </summary>
    Task StopAsync(Process process, string modelName, ILogger logger);

    /// <summary>
    /// Shuts down all tracked processes on application shutdown.
    /// </summary>
    Task ShutdownAllAsync(IEnumerable<Process> processes, ILogger logger);
}
