using Microsoft.Extensions.Options;
using System.Diagnostics;
using LlaModem.Config;

namespace LlaModem.Services;

public sealed class ModelManager : IMetaModelManager
{
    private readonly AppConfig _config;
    private readonly RouterConfig.TimeoutConfig _timeouts;
    private readonly ILogger<ModelManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HealthChecker _healthChecker;
    private readonly ProcessKiller _processKiller;
    private readonly DefaultModelLauncher _launcher;
    private readonly IModelRepository _repository;
    private readonly GpuMemoryChecker _gpuChecker;

    public ModelManager(
        IOptions<AppConfig> config,
        IOptions<RouterConfig> routerConfig,
        ILogger<ModelManager> logger,
        IHttpClientFactory httpClientFactory,
        HealthChecker healthChecker,
        ProcessKiller processKiller,
        DefaultModelLauncher launcher,
        IModelRepository repository,
        GpuMemoryChecker gpuChecker)
    {
        _config = config.Value;
        _timeouts = routerConfig.Value.Timeouts;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _healthChecker = healthChecker;
        _processKiller = processKiller;
        _launcher = launcher;
        _repository = repository;
        _gpuChecker = gpuChecker;
    }

    /// <summary>
    /// Gets the current active model name by querying the repository.
    /// </summary>
    public async Task<string?> GetActiveModelNameAsync(CancellationToken ct = default)
    {
        var state = await _repository.GetStateAsync("active", ct);
        return state?.ModelName;
    }

    /// <summary>
    /// True if no model is currently active.
    /// </summary>
    public async Task<bool> IsIdleAsync(CancellationToken ct = default)
    {
        var state = await _repository.GetStateAsync("active", ct);
        return state is null;
    }



    /// <summary>
    /// Ensures the requested model is running. Starts it if needed, switches if different model is active.
    /// </summary>
    public async Task EnsureModelAsync(string modelName, ModelLaunchParams? launchParams = null, CancellationToken ct = default)
    {
        var modelConfig = _config.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            var available = string.Join(", ", _config.Models.Keys);
            throw new InvalidOperationException(
                $"Model '{modelName}' not found. Available models: {available}");
        }

        // Check 1: Is this model active AND is the backend healthy?
        var currentState = await _repository.GetStateAsync(modelName, ct);
        var backendUrl = modelConfig.BackendUrl ?? _config.BackendUrl;
        if (currentState is not null)
        {
            var backendHealthy = await _launcher.IsModelRunningV2(backendUrl, ct);
            if (backendHealthy)
            {
                _logger.LogDebug("Model '{Model}' is already running and healthy", modelName);
                return;
            }
            // Backend not responding — model likely died, fall through to restart
        }

        // Check 2: Repository state — process still tracked and alive?
        if (currentState is not null)
        {
            try
            {
                var process = Process.GetProcessById(currentState.ProcessId);
                if (!process.HasExited)
                {
                    _logger.LogDebug("Model '{Model}' is already running", modelName);
                    return;
                }
            }
            catch (ArgumentException)
            {
                // Process no longer exists — fall through to restart
            }
        }

        // All checks failed — model needs to be started
        await SwitchModelAsync(modelName, modelConfig, launchParams, ct);
    }

    /// <summary>
    /// Gracefully stops the currently active model.
    /// </summary>
    public async Task StopActiveModelAsync(CancellationToken ct = default)
    {
        var state = await _repository.GetStateAsync("active", ct);
        if (state is null)
        {
            _logger.LogDebug("No active model to stop");
            return;
        }

        try
        {
            var process = Process.GetProcessById(state.ProcessId);
            await _processKiller.StopAsync(process, state.ModelName, _logger);
        }
        catch (ArgumentException)
        {
            // Process no longer exists — just clear state
            await _repository.ClearStateAsync(ct);
        }
    }

    private async Task SwitchModelAsync(string modelName, ModelConfig modelConfig, ModelLaunchParams? launchParams = null, CancellationToken ct = default)
    {
        // Stop current model if different AND target model is not exclusive
        var currentModelName = await GetActiveModelNameAsync(ct);
        if (currentModelName != modelName && !modelConfig.Exclusive)
        {
            await StopActiveModelAsync(ct);
        }

        await StartModelAsync(modelName, modelConfig, launchParams, ct);
    }

    private async Task StartModelAsync(string modelName, ModelConfig modelConfig, ModelLaunchParams? launchParams = null, CancellationToken ct = default)
    {
        var backendUrl = modelConfig.BackendUrl ?? _config.BackendUrl;

        // VRAM check — disabled, see below
        // CheckVramAvailability(modelName);

        _logger.LogInformation(
            "Starting model '{Model}' via script '{Script}' on backend {Url}",
            modelName, modelConfig.StartScript, backendUrl);

        var process = await _launcher.StartAsync(modelName, modelConfig.StartScript, launchParams);
        if (process is null)
        {
            _logger.LogError("Failed to launch process for model '{Model}'", modelName);
            throw new InvalidOperationException($"Failed to start model '{modelName}'");
        }

        // Capture stdout/stderr for logging
        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogDebug("[{Model}] {Data}", modelName, e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogDebug("[{Model} ERR] {Data}", modelName, e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for health check
        var healthy = await WaitForHealthCheckAsync(backendUrl, ct);
        if (!healthy)
        {
            _logger.LogError("Model '{Model}' failed health check on {Url}", modelName, backendUrl);
            await StopActiveModelAsync(ct);
            throw new InvalidOperationException(
                $"Model '{modelName}' failed to become healthy within timeout on {backendUrl}");
        }

        _logger.LogInformation("Model '{Model}' is now active and healthy on {Url}", modelName, backendUrl);
    }

    private async Task<bool> WaitForHealthCheckAsync(string backendUrl, CancellationToken ct = default)
    {
        var healthPath = "/health";
        var url = $"{backendUrl.TrimEnd('/')}{healthPath}";
        var (success, reason) = await _healthChecker.PollAsync(
            url,
            TimeSpan.FromMinutes(_timeouts.HealthCheckPollTimeoutMinutes),
            TimeSpan.FromMilliseconds(_timeouts.HealthCheckPollDelayMs),
            ct);
        if (!success)
            _logger.LogWarning("Health check failed for {Url}: {Reason}", url, reason);
        return success;
    }

    /// <summary>
    /// Checks available VRAM via nvidia-smi. Disabled — kept as a reference.
    /// </summary>
    private void CheckVramAvailability(string modelName)
    {
        var (isSufficient, errorMessage) = _gpuChecker.CheckAvailableResources();
        if (!isSufficient)
        {
            _logger.LogError("Cannot start model '{Model}': {Error}", modelName, errorMessage);
            throw new InvalidOperationException(
                $"Insufficient resources to start '{modelName}': {errorMessage}");
        }
    }

    /// <summary>
    /// Called on application shutdown to ensure all tracked processes are terminated.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _launcher.ShutdownAllAsync(_logger, ct);
        await _repository.ClearStateAsync(ct);
    }
}
