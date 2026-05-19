using LlaModem.Config;
using LlaModem.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LlaModem.Services;

/// <summary>
/// Dashboard service — composes other services to build dashboard views.
/// Follows the principle "Separate what changes from what stays stable":
/// presentation logic (dashboard) is separate from business logic (model lifecycle).
/// </summary>
public sealed class DashboardService
{
    private readonly AppConfig _config;
    private readonly ModelManager _modelManager;
    private readonly ModelMetricsService _metricsService;
    private readonly HealthChecker _healthChecker;
    private readonly IModelRepository _repository;
    private readonly string _paramsFilePath;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IOptions<AppConfig> config,
        ModelManager modelManager,
        ModelMetricsService metricsService,
        HealthChecker healthChecker,
        IModelRepository repository,
        IOptions<RouterConfig> routerConfig,
        ILogger<DashboardService> logger)
    {
        _config = config.Value;
        _modelManager = modelManager;
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
    /// Gets the current process state for a model.
    /// </summary>
    public async Task<ModelProcessState?> GetModelStateAsync(string modelName, CancellationToken ct = default)
    {
        return await _repository.GetStateAsync(modelName, ct);
    }

    /// <summary>
    /// Updates model parameters and persists to dashboard_params.json.
    /// This is the only business logic DashboardService owns — parameter persistence.
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
    /// Loads parameters from dashboard_params.json or returns defaults.
    /// </summary>
    public ModelLaunchParams LoadParams(string modelName)
    {
        if (!File.Exists(_paramsFilePath))
        {
            return ModelLaunchParams.Defaults;
        }

        try
        {
            var json = File.ReadAllText(_paramsFilePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty(modelName, out var modelElement))
            {
                return ModelLaunchParams.Defaults;
            }

            var temp = TryGetDecimal(modelElement, "Temperature");
            var topP = TryGetDecimal(modelElement, "TopP");
            var topK = TryGetDecimal(modelElement, "TopK");
            var minP = TryGetDecimal(modelElement, "MinP");
            var presence = TryGetDecimal(modelElement, "PresencePenalty");
            var repetition = TryGetDecimal(modelElement, "RepetitionPenalty");

            // Fall back to defaults for any null values
            return new ModelLaunchParams(
                temp ?? ModelLaunchParams.Defaults.Temperature,
                topP ?? ModelLaunchParams.Defaults.TopP,
                topK ?? ModelLaunchParams.Defaults.TopK,
                minP ?? ModelLaunchParams.Defaults.MinP,
                presence ?? ModelLaunchParams.Defaults.PresencePenalty,
                repetition ?? ModelLaunchParams.Defaults.RepetitionPenalty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load params for model '{Model}'", modelName);
            return ModelLaunchParams.Defaults;
        }
    }

    private static decimal? TryGetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            // Round to 2 decimal places (llama-server precision limit)
            return Math.Round(value.GetDecimal(), 2);
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
