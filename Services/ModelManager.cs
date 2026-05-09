using Microsoft.Extensions.Options;
using System.Diagnostics;
using LlaModem.Config;

namespace LlaModem.Services;

public class ModelManager
{
    private readonly AppConfig _config;
    private readonly RouterConfig.TimeoutConfig _timeouts;
    private readonly ILogger<ModelManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HealthChecker _healthChecker;
    private readonly ProcessKiller _processKiller;
    private readonly DefaultModelLauncher _launcher;
    private readonly GpuMemoryChecker _gpuChecker;
    private readonly object _lock = new();

    private string? _activeModelName;
    private Process? _activeProcess;

    public string? ActiveModelName => _activeModelName;
    public bool IsIdle => _activeModelName is null;

    public ModelManager(
        IOptions<AppConfig> config,
        IOptions<RouterConfig> routerConfig,
        ILogger<ModelManager> logger,
        IHttpClientFactory httpClientFactory,
        HealthChecker healthChecker,
        ProcessKiller processKiller,
        DefaultModelLauncher launcher,
        GpuMemoryChecker gpuChecker)
    {
        _config = config.Value;
        _timeouts = routerConfig.Value.Timeouts;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _healthChecker = healthChecker;
        _processKiller = processKiller;
        _launcher = launcher;
        _gpuChecker = gpuChecker;
    }

    /// <summary>
    /// Ensures the requested model is running. Starts it if needed, switches if different model is active.
    /// </summary>
    public async Task EnsureModelAsync(string modelName, ModelLaunchParams? launchParams = null)
    {
        var modelConfig = _config.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            var available = string.Join(", ", _config.Models.Keys);
            throw new InvalidOperationException(
                $"Model '{modelName}' not found. Available models: {available}");
        }

        // Check 1: Is this model active AND is the backend healthy?
        if (_launcher.IsModelRunning(modelName))
        {
            var backendHealthy = await _launcher.IsModelRunningV2(_config.BackendUrl);
            if (backendHealthy)
            {
                _logger.LogDebug("Model '{Model}' is already running and healthy", modelName);
                lock (_lock)
                {
                    _activeModelName = modelName;
                }
                return;
            }
            // Backend not responding — model likely died, fall through to restart
        }

        // Check 2: Internal state — process still tracked and alive?
        lock (_lock)
        {
            if (_activeModelName == modelName && _activeProcess is not null && !_activeProcess.HasExited)
            {
                _logger.LogDebug("Model '{Model}' is already running", modelName);
                return;
            }
        }

        // All checks failed — model needs to be started
        await SwitchModelAsync(modelName, modelConfig, launchParams);
    }

    /// <summary>
    /// Gracefully stops the currently active model.
    /// </summary>
    public async Task StopActiveModelAsync()
    {
        Process? process;
        string? modelName;

        lock (_lock)
        {
            if (_activeModelName is null)
            {
                _logger.LogDebug("No active model to stop");
                return;
            }
            process = _activeProcess;
            modelName = _activeModelName;
            _activeProcess = null;
            _activeModelName = null;
        }

        if (process is not null)
        {
            await _processKiller.StopAsync(process, modelName, _logger);
        }
        else
        {
            // Process reference was lost (e.g. backend detected healthy but process not tracked).
            // Look it up by model name and stop it.
            await _launcher.StopModelByNameAsync(modelName, _logger);
        }
    }

    private async Task SwitchModelAsync(string modelName, ModelConfig modelConfig, ModelLaunchParams? launchParams = null)
    {
        // Stop current model if different
        if (_activeModelName != modelName)
        {
            await StopActiveModelAsync();
        }

        await StartModelAsync(modelName, modelConfig, launchParams);
    }

    private async Task StartModelAsync(string modelName, ModelConfig modelConfig, ModelLaunchParams? launchParams = null)
    {
        // VRAM check — disabled, see below
        // CheckVramAvailability(modelName);

        _logger.LogInformation(
            "Starting model '{Model}' via script '{Script}' on backend {Url}",
            modelName, modelConfig.StartScript, _config.BackendUrl);

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

        lock (_lock)
        {
            _activeProcess = process;
            _activeModelName = modelName;
        }

        // Wait for health check
        var healthy = await WaitForHealthCheckAsync(_config.BackendUrl);
        if (!healthy)
        {
            _logger.LogError("Model '{Model}' failed health check on {Url}", modelName, _config.BackendUrl);
            await StopActiveModelAsync();
            throw new InvalidOperationException(
                $"Model '{modelName}' failed to become healthy within timeout on {_config.BackendUrl}");
        }

        _logger.LogInformation("Model '{Model}' is now active and healthy on {Url}", modelName, _config.BackendUrl);
    }

    private async Task<bool> WaitForHealthCheckAsync(string backendUrl)
    {
        var healthPath = "/health";
        var url = $"{backendUrl.TrimEnd('/')}{healthPath}";
        var (success, reason) = await _healthChecker.PollAsync(
            url,
            TimeSpan.FromMinutes(_timeouts.HealthCheckPollTimeoutMinutes),
            TimeSpan.FromMilliseconds(_timeouts.HealthCheckPollDelayMs));
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

    // No explicit disposal needed. The DI container handles the lifecycle,
    // and Program.cs calls launcher.ShutdownAllAsync() on application shutdown.
}
