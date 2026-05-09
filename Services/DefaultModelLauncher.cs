using System.Collections.Concurrent;
using System.Diagnostics;

namespace LlaModem.Services;

public class DefaultModelLauncher
{
    private const string PowerShellExe = "powershell";
    private const string PowerShellTitle = "LlaModem";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HealthChecker _healthChecker;
    private readonly ProcessKiller _processKiller;

    private string? _activeModelName;
    /// <summary>
    /// Thread-safe process tracking using ConcurrentHashSet pattern.
    /// </summary>
    private readonly ConcurrentDictionary<int, Process> _trackedProcesses = new();

    public DefaultModelLauncher(
        IHttpClientFactory httpClientFactory,
        HealthChecker healthChecker,
        ProcessKiller processKiller)
    {
        _httpClientFactory = httpClientFactory;
        _healthChecker = healthChecker;
        _processKiller = processKiller;
    }

    public async Task<Process?> StartAsync(string modelName, string scriptPath, ModelLaunchParams? launchParams = null)
    {
        var workingDir = Path.GetDirectoryName(scriptPath);

        // Set window title to a constant so we can identify the process later
        var escapedScript = scriptPath.Replace("'", "''");

        // Build optional launch params suffix
        var paramParts = new List<string>();
        if (launchParams?.Temperature.HasValue == true) paramParts.Add($"-Temperature {launchParams.Temperature}");
        if (launchParams?.TopP.HasValue == true) paramParts.Add($"-TopP {launchParams.TopP}");
        if (launchParams?.TopK.HasValue == true) paramParts.Add($"-TopK {launchParams.TopK}");
        if (launchParams?.MinP.HasValue == true) paramParts.Add($"-MinP {launchParams.MinP}");
        if (launchParams?.PresencePenalty.HasValue == true) paramParts.Add($"-PresencePenalty {launchParams.PresencePenalty}");
        if (launchParams?.RepetitionPenalty.HasValue == true) paramParts.Add($"-RepetitionPenalty {launchParams.RepetitionPenalty}");
        var paramSuffix = paramParts.Count > 0 ? " " + string.Join(" ", paramParts) : string.Empty;

        var arguments =
            $"-ExecutionPolicy Bypass -Command \"$Host.UI.RawUI.WindowTitle = '{PowerShellTitle}'; & '{escapedScript}'{paramSuffix}\"";
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
            _trackedProcesses[process.Id] = process;
            _activeModelName = modelName;
        }

        return process;
    }

    public bool IsModelRunning(string modelName)
    {
        return string.Equals(_activeModelName, modelName, StringComparison.OrdinalIgnoreCase);
    }

    private Process? FindProcessByTitle(string _ = "")
    {
        var psProcesses = Process.GetProcessesByName("powershell");
        return Array.Find(psProcesses, p =>
            p.MainWindowTitle.Contains(PowerShellTitle, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsModelRunningV2(string backendUrl, CancellationToken cancellationToken = default)
    {
        var healthUrl = $"{backendUrl.TrimEnd('/')}/health";
        var (success, _) = await _healthChecker.CheckAsync(healthUrl, cancellationToken);
        return success;
    }

    public async Task StopAsync(Process process, string modelName, ILogger logger)
    {
        // Remove from tracked processes before stopping
        _trackedProcesses.TryRemove(process.Id, out _);

        await _processKiller.StopAsync(process, modelName, logger);

        // Clear the active model reference
        _activeModelName = null;
    }

    public async Task ShutdownAllAsync(ILogger logger)
    {
        // Atomically snapshot and clear tracked processes
        var processesToKill = _trackedProcesses.Values.ToArray();
        _trackedProcesses.Clear();

        // Clear the active model reference
        _activeModelName = null;
        await _processKiller.ShutdownAllAsync(processesToKill, logger);
    }

    public async Task StopModelByNameAsync(string modelName, ILogger logger)
    {
        var target = FindProcessByTitle(modelName);

        if (target is null || target.HasExited)
        {
            logger.LogDebug("No running process found for model '{Model}'", modelName);
            return;
        }

        logger.LogInformation(
            "Found process for model '{Model}' (PID: {Pid}) — stopping it",
            modelName, target.Id);

        await _processKiller.StopAsync(target, modelName, logger);
        // Clear the active model reference
        _activeModelName = null;
    }
}
