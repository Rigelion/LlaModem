using Microsoft.Extensions.Options;
using System.Diagnostics;
using LlaModem.Config;

namespace LlaModem.Services;

public class ModelManager
{
    private readonly AppConfig _config;
    private readonly ILogger<ModelManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHealthChecker _healthChecker;
    private readonly IProcessKiller _processKiller;
    private readonly IModelLauncher _launcher;
    private readonly IGpuMemoryChecker _gpuChecker;
    private readonly object _lock = new();

    private string? _activeModelName;
    private Process? _activeProcess;

    public string? ActiveModelName => _activeModelName;
    public bool IsIdle => _activeModelName is null;

    public ModelManager(
        IOptions<AppConfig> config,
        ILogger<ModelManager> logger,
        IHttpClientFactory httpClientFactory,
        IHealthChecker healthChecker,
        IProcessKiller processKiller,
        IModelLauncher? launcher = null,
        IGpuMemoryChecker? gpuChecker = null)
    {
        _config = config.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _healthChecker = healthChecker;
        _processKiller = processKiller;
        _launcher = launcher ?? new DefaultModelLauncher(httpClientFactory, healthChecker, processKiller);
        _gpuChecker = gpuChecker ?? new GpuMemoryChecker();
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

        // Check 1: Is a PowerShell window with this model title already open?
        if (_launcher.IsModelRunning(modelName))
        {
            _logger.LogDebug("PowerShell window for model '{Model}' is already running", modelName);
            return;
        }

        // Check 2: Is the backend URL responding? (llama-server may be running but title check missed it)
        var backendHealthy = await _launcher.IsModelRunningV2(modelConfig.BackendUrl);
        if (backendHealthy)
        {
            _logger.LogDebug("Backend for model '{Model}' at {Url} is healthy", modelName, modelConfig.BackendUrl);
            lock (_lock)
            {
                _activeProcess = null; // Will be refreshed on next request
                _activeModelName = modelName;
            }
            return;
        }

        // Check 3: Internal state — process still tracked and alive?
        lock (_lock)
        {
            if (_activeModelName == modelName && _activeProcess is not null && !_activeProcess.HasExited)
            {
                _logger.LogDebug("Model '{Model}' is already running on {Url}", modelName, modelConfig.BackendUrl);
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
            if (_activeProcess is null || _activeModelName is null)
            {
                _logger.LogDebug("No active model to stop");
                return;
            }
            process = _activeProcess;
            modelName = _activeModelName;
            _activeProcess = null;
            _activeModelName = null;
        }

        await _processKiller.StopAsync(process, modelName, _logger);
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
        // VRAM check — only reached when no model is running (all detection checks failed above)
        var (isSufficient, errorMessage) = _gpuChecker.CheckAvailableResources();
        if (!isSufficient)
        {
            _logger.LogError("Cannot start model '{Model}': {Error}", modelName, errorMessage);
            throw new InvalidOperationException(
                $"Insufficient resources to start '{modelName}': {errorMessage}");
        }

        _logger.LogInformation(
            "Starting model '{Model}' via script '{Script}' on backend {Url}",
            modelName, modelConfig.StartScript, modelConfig.BackendUrl);

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
        var healthy = await WaitForHealthCheckAsync(modelConfig.BackendUrl);
        if (!healthy)
        {
            _logger.LogError("Model '{Model}' failed health check on {Url}", modelName, modelConfig.BackendUrl);
            await StopActiveModelAsync();
            throw new InvalidOperationException(
                $"Model '{modelName}' failed to become healthy within timeout on {modelConfig.BackendUrl}");
        }

        _logger.LogInformation("Model '{Model}' is now active and healthy on {Url}", modelName, modelConfig.BackendUrl);
    }

    private async Task<bool> WaitForHealthCheckAsync(string backendUrl)
    {
        var healthPath = "/health";
        var url = $"{backendUrl.TrimEnd('/')}{healthPath}";
        return await _healthChecker.PollAsync(url, TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(500));
    }

    // No explicit disposal needed. The DI container handles the lifecycle,
    // and Program.cs calls launcher.ShutdownAllAsync() on application shutdown.
}
