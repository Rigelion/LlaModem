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
    private readonly IModelRepository _repository;

    /// <summary>
    /// Thread-safe process tracking via IModelRepository abstraction.
    /// </summary>

    public DefaultModelLauncher(
        IHttpClientFactory httpClientFactory,
        HealthChecker healthChecker,
        ProcessKiller processKiller,
        IModelRepository repository)
    {
        _httpClientFactory = httpClientFactory;
        _healthChecker = healthChecker;
        _processKiller = processKiller;
        _repository = repository;
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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);

        // Track the process for shutdown cleanup
        if (process is not null)
        {
            var state = new ModelProcessState(modelName, process.Id, DateTimeOffset.UtcNow);
            await _repository.SetStateAsync(state);
        }

        return process;
    }

    public async Task<bool> IsModelRunningAsync(string modelName, CancellationToken ct = default)
    {
        var state = await _repository.GetStateAsync(modelName, ct);
        return state is not null && state.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ModelProcessState?> GetCurrentStateAsync(string modelName, CancellationToken ct = default)
    {
        return await _repository.GetStateAsync(modelName, ct);
    }

    public async Task<bool> IsModelRunningV2(string backendUrl, CancellationToken cancellationToken = default)
    {
        var healthUrl = $"{backendUrl.TrimEnd('/')}/health";
        var (success, _) = await _healthChecker.CheckAsync(healthUrl, cancellationToken);
        return success;
    }

    public async Task StopAsync(Process process, string modelName, ILogger logger, CancellationToken ct = default)
    {
        // Clear repository state before stopping
        await _repository.ClearStateAsync(ct);

        await _processKiller.StopAsync(process, modelName, logger);
    }

    public async Task ShutdownAllAsync(ILogger logger, CancellationToken ct = default)
    {
        // Atomically snapshot all tracked processes
        var allStates = await _repository.GetAllStatesAsync(ct);
        var processesToKill = new List<Process>();

        foreach (var state in allStates)
        {
            try
            {
                var process = Process.GetProcessById(state.ProcessId);
                if (!process.HasExited)
                    processesToKill.Add(process);
            }
            catch (ArgumentException)
            {
                // Process already exited
            }
        }

        await _repository.ClearStateAsync(ct);
        await _processKiller.ShutdownAllAsync(processesToKill, logger);
    }

    public async Task StopModelByNameAsync(string modelName, ILogger logger, CancellationToken ct = default)
    {
        var state = await GetCurrentStateAsync(modelName, ct);

        if (state is null)
        {
            logger.LogDebug("No running process found for model '{Model}'", modelName);
            return;
        }

        Process? target;
        try
        {
            target = Process.GetProcessById(state.ProcessId);
            if (target.HasExited)
            {
                logger.LogDebug("Process for model '{Model}' (PID: {Pid}) has exited", modelName, state.ProcessId);
                await _repository.ClearStateAsync(ct);
                return;
            }
        }
        catch (ArgumentException)
        {
            logger.LogDebug("Process for model '{Model}' (PID: {Pid}) not found", modelName, state.ProcessId);
            await _repository.ClearStateAsync(ct);
            return;
        }

        logger.LogInformation(
            "Found process for model '{Model}' (PID: {Pid}) — stopping it",
            modelName, target.Id);

        await _processKiller.StopAsync(target, modelName, logger);
        await _repository.ClearStateAsync(ct);
    }
}
