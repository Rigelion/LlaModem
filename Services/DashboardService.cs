using LlaModem.Config;
using LlaModem.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlaModem.Services;

/// <summary>
/// Service for admin dashboard operations.
/// Provides model status, metrics, and parameter management.
/// </summary>
public sealed class DashboardService
{
    private readonly AppConfig _config;
    private readonly ModelManager _modelManager;
    private readonly DefaultModelLauncher _launcher;
    private readonly ModelMetricsService _metricsService;
    private readonly HealthChecker _healthChecker;
    private readonly IModelRepository _repository;
    private readonly string _paramsFilePath;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IOptions<AppConfig> config,
        ModelManager modelManager,
        DefaultModelLauncher launcher,
        ModelMetricsService metricsService,
        HealthChecker healthChecker,
        IModelRepository repository,
        IOptions<RouterConfig> routerConfig,
        ILogger<DashboardService> logger)
    {
        _config = config.Value;
        _modelManager = modelManager;
        _launcher = launcher;
        _metricsService = metricsService;
        _healthChecker = healthChecker;
        _repository = repository;
        _paramsFilePath = Path.Combine(AppContext.BaseDirectory, "dashboard_params.json");
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard items for all configured models.
    /// </summary>
    public async Task<ModelDashboardItem[]> GetAllModelsAsync(CancellationToken ct = default)
    {
        var items = new List<ModelDashboardItem>();

        foreach (var (modelName, modelConfig) in _config.Models)
        {
            var item = await BuildModelItemAsync(modelName, modelConfig, ct);
            items.Add(item);
        }

        return items.ToArray();
    }

    /// <summary>
    /// Gets dashboard item for a specific model.
    /// </summary>
    public async Task<ModelDashboardItem?> GetModelAsync(string modelName, CancellationToken ct = default)
    {
        if (!_config.Models.ContainsKey(modelName))
        {
            return null;
        }

        var modelConfig = _config.Models[modelName];
        return await BuildModelItemAsync(modelName, modelConfig, ct);
    }

    /// <summary>
    /// Starts a model with optional parameter overrides.
    /// </summary>
    public async Task<StartModelResult> StartModelAsync(
        string modelName,
        StartModelRequest? request = null,
        CancellationToken ct = default)
    {
        if (!_config.Models.ContainsKey(modelName))
        {
            return new StartModelResult(
                false,
                null,
                null,
                null,
                $"Model '{modelName}' not found in configuration");
        }

        var modelConfig = _config.Models[modelName];
        var launchParams = request?.ToLaunchParams() ?? LoadParams(modelName);

        try
        {
            await _modelManager.EnsureModelAsync(modelName, launchParams, ct);
            _metricsService.StartSession(modelName, DateTimeOffset.UtcNow);

            var state = await _repository.GetStateAsync(modelName, ct);
            var processId = state?.ProcessId;
            var startedAt = state?.StartedAt;

            return new StartModelResult(
                Success: true,
                ProcessId: processId,
                StartedAt: startedAt,
                Error: null,
                Message: "Model already running");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start model '{Model}'", modelName);
            return new StartModelResult(
                false,
                null,
                null,
                null,
                ex.Message);
        }
    }

    /// <summary>
    /// Stops a model.
    /// </summary>
    public async Task<StopModelResult> StopModelAsync(string modelName, CancellationToken ct = default)
    {
        if (!_config.Models.ContainsKey(modelName))
        {
            return new StopModelResult(false, null, null, $"Model '{modelName}' not found");
        }

        var activeModel = await _modelManager.GetActiveModelNameAsync(ct);
        if (activeModel != modelName)
        {
            return new StopModelResult(false, null, null, $"Model '{modelName}' is not active");
        }

        try
        {
            _metricsService.EndSession(modelName);
            await _modelManager.StopActiveModelAsync(ct);

            return new StopModelResult(true, null, null, $"Model '{modelName}' stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop model '{Model}'", modelName);
            return new StopModelResult(
                false,
                null,
                null,
                $"Failed to stop model: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates model parameters and persists to dashboard_params.json.
    /// </summary>
    public ParameterUpdateResponse UpdateParamsAsync(string modelName, UpdateModelParamsRequest request)
    {
        if (!_config.Models.ContainsKey(modelName))
        {
            throw new InvalidOperationException($"Model '{modelName}' not found");
        }

        var existingParams = LoadParams(modelName);
        var updatedParams = existingParams with
        {
            Temperature = request.Temperature ?? existingParams.Temperature,
            TopP = request.TopP ?? existingParams.TopP,
            TopK = request.TopK ?? existingParams.TopK,
            MinP = request.MinP ?? existingParams.MinP,
            PresencePenalty = request.PresencePenalty ?? existingParams.PresencePenalty,
            RepetitionPenalty = request.RepetitionPenalty ?? existingParams.RepetitionPenalty
        };

        var paramsDict = new Dictionary<string, object>
        {
            [modelName] = new
            {
                Temperature = updatedParams.Temperature,
                TopP = updatedParams.TopP,
                TopK = updatedParams.TopK,
                MinP = updatedParams.MinP,
                PresencePenalty = updatedParams.PresencePenalty,
                RepetitionPenalty = updatedParams.RepetitionPenalty
            }
        };

        var json = JsonSerializer.Serialize(paramsDict, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_paramsFilePath, json);

        _logger.LogInformation("Updated parameters for model '{Model}'", modelName);

        return new ParameterUpdateResponse(
            "Parameters updated successfully",
            true,
            updatedParams);
    }

    /// <summary>
    /// Gets health check status for a model.
    /// </summary>
    public async Task<HealthCheckResponse> GetHealthAsync(string modelName, CancellationToken ct = default)
    {
        var activeModel = await _modelManager.GetActiveModelNameAsync(ct);
        if (activeModel != modelName)
        {
            throw new InvalidOperationException($"Model '{modelName}' is not running");
        }

        var healthUrl = $"{_config.BackendUrl.TrimEnd('/')}/health";
        var startTime = DateTimeOffset.UtcNow;
        var (success, reason) = await _healthChecker.CheckAsync(healthUrl, ct);
        var responseTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

        return new HealthCheckResponse(
            IsHealthy: success,
            HealthUrl: healthUrl,
            LastCheckedAt: DateTimeOffset.UtcNow,
            ResponseTimeMs: responseTime);
    }

    /// <summary>
    /// Loads parameters from dashboard_params.json or returns null.
    /// </summary>
    private ModelLaunchParams LoadParams(string modelName)
    {
        if (!File.Exists(_paramsFilePath))
        {
            return new ModelLaunchParams(null, null, null, null, null, null);
        }

        try
        {
            var json = File.ReadAllText(_paramsFilePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty(modelName, out var modelElement))
            {
                return new ModelLaunchParams(null, null, null, null, null, null);
            }

            var temp = TryGetDouble(modelElement, "Temperature");
            var topP = TryGetDouble(modelElement, "TopP");
            var topK = TryGetDouble(modelElement, "TopK");
            var minP = TryGetDouble(modelElement, "MinP");
            var presence = TryGetDouble(modelElement, "PresencePenalty");
            var repetition = TryGetDouble(modelElement, "RepetitionPenalty");

            return new ModelLaunchParams(temp, topP, topK, minP, presence, repetition);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load params for model '{Model}'", modelName);
            return new ModelLaunchParams(null, null, null, null, null, null);
        }
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return null;
    }

    private async Task<ModelDashboardItem> BuildModelItemAsync(
        string modelName,
        ModelConfig modelConfig,
        CancellationToken ct)
    {
        var state = await _repository.GetStateAsync(modelName, ct);
        var isRunning = state is not null;
        var activeModel = await _modelManager.GetActiveModelNameAsync(ct);
        var isActive = activeModel == modelName;

        // Get last request timestamp
        var lastRequestAt = _metricsService.GetLastRequestAt(modelName);

        // Determine status
        string status;
        if (!isRunning)
        {
            status = "stopped";
        }
        else if (!isActive)
        {
            status = "stopped";
        }
        else
        {
            // Check if idle (no requests in last 10 seconds)
            var idleThreshold = TimeSpan.FromSeconds(10);
            status = lastRequestAt.HasValue && (DateTimeOffset.UtcNow - lastRequestAt.Value) > idleThreshold
                ? "running_idle"
                : "running_active";
        }

        // Get metrics
        var currentTps = isRunning ? _metricsService.GetCurrentTokensPerSecond(modelName) : 0;
        var avgTps = _metricsService.GetAverageTokensPerSession(modelName);

        // Load params
        var paramsObj = LoadParams(modelName);

        // Get health status
        bool? isHealthy = null;
        if (isRunning && isActive)
        {
            var healthUrl = $"{_config.BackendUrl.TrimEnd('/')}/health";
            var (success, _) = await _healthChecker.CheckAsync(healthUrl, ct);
            isHealthy = success;
        }

        // Get last error (from session or repository)
        string? lastError = null;

        return new ModelDashboardItem(
            Name: modelName,
            Status: status,
            CurrentTokensPerSecond: currentTps,
            AverageTokensPerSession: avgTps,
            Parameters: paramsObj,
            ScriptPath: modelConfig.StartScript,
            ProcessId: state?.ProcessId,
            StartedAt: state?.StartedAt,
            LastRequestAt: lastRequestAt,
            IsHealthy: isHealthy,
            LastError: lastError);
    }
}

/// <summary>
/// Result of starting a model.
/// </summary>
public sealed record StartModelResult(
    bool Success,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    string? Error,
    string Message)
{
    public ModelDashboardItem ToDashboardItem(string modelName, ModelConfig config)
    {
        return new ModelDashboardItem(
            Name: modelName,
            Status: "running_active",
            CurrentTokensPerSecond: 0,
            AverageTokensPerSession: null,
            Parameters: new ModelLaunchParams(null, null, null, null, null, null),
            ScriptPath: config.StartScript,
            ProcessId: ProcessId,
            StartedAt: StartedAt,
            LastRequestAt: null,
            IsHealthy: null,
            LastError: Error);
    }
}

/// <summary>
/// Result of stopping a model.
/// </summary>
public sealed record StopModelResult(
    bool Success,
    int? ProcessId,
    DateTimeOffset? StoppedAt,
    string Message)
{
    public bool HasError => !Success;
}
