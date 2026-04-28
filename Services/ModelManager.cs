using Microsoft.Extensions.Options;
using System.Diagnostics;
using LlaModem.Config;

namespace LlaModem.Services;

public class ModelManager : IDisposable
{
    private readonly AppConfig _config;
    private readonly ILogger<ModelManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly object _lock = new();

    private string? _activeModelName;
    private Process? _activeProcess;

    public string? ActiveModelName => _activeModelName;
    public bool IsIdle => _activeModelName is null;

    public ModelManager(
        IOptions<AppConfig> config,
        ILogger<ModelManager> logger)
    {
        _config = config.Value;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// Ensures the requested model is running. Starts it if needed, switches if different model is active.
    /// </summary>
    public async Task EnsureModelAsync(string modelName)
    {
        var modelConfig = _config.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            var available = string.Join(", ", _config.Models.Keys);
            throw new InvalidOperationException(
                $"Model '{modelName}' not found. Available models: {available}");
        }

        lock (_lock)
        {
            if (_activeModelName == modelName && _activeProcess is not null && !_activeProcess.HasExited)
            {
                _logger.LogDebug("Model '{Model}' is already running on {Url}", modelName, modelConfig.BackendUrl);
                return;
            }
        }

        // Switch or start — outside lock to avoid holding it during async operations
        await SwitchModelAsync(modelName, modelConfig);
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

        _logger.LogInformation("Stopping model '{Model}' (PID: {Pid})", modelName, process.Id);

        try
        {
            if (!process.HasExited)
            {
                // Graceful shutdown: try to terminate first
                process.Kill(false);

                // Wait up to 5 seconds for graceful exit
                var exited = process.WaitForExit(5000);
                if (!exited)
                {
                    _logger.LogWarning("Model '{Model}' did not exit gracefully within 5s, force killing", modelName);
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping model '{Model}'", modelName);
        }

        _logger.LogInformation("Model '{Model}' stopped", modelName);
    }

    private async Task SwitchModelAsync(string modelName, ModelConfig modelConfig)
    {
        // Stop current model if different
        if (_activeModelName != modelName)
        {
            await StopActiveModelAsync();
        }

        await StartModelAsync(modelName, modelConfig);
    }

    private async Task StartModelAsync(string modelName, ModelConfig modelConfig)
    {
        _logger.LogInformation(
            "Starting model '{Model}' via script '{Script}' on backend {Url}",
            modelName, modelConfig.StartScript, modelConfig.BackendUrl);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{modelConfig.StartScript}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process process;
        try
        {
            process = Process.Start(psi)
                      ?? throw new InvalidOperationException($"Failed to start process for model '{modelName}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch PowerShell script for model '{Model}'", modelName);
            throw new InvalidOperationException(
                $"Failed to start model '{modelName}': {ex.Message}", ex);
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
        var healthPath = "/health"; // Common llama-server health endpoint
        var url = $"{backendUrl.TrimEnd('/')}{healthPath}";
        var timeout = TimeSpan.FromMinutes(2);
        var delay = TimeSpan.FromMilliseconds(500);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Backend not ready yet — retry
            }

            await Task.Delay(delay);
        }

        return false;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        StopActiveModelAsync().GetAwaiter().GetResult();
    }
}
