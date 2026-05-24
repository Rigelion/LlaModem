using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public sealed class ModelProxyHandler : IModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly IMetaModelManager _modelManager;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly DashboardService _dashboardService;
    private readonly IRequestForwarder _forwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        IMetaModelManager modelManager,
        SystemIdleTracker systemIdleTracker,
        DashboardService dashboardService,
        IRequestForwarder forwarder,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelProxyHandler> logger)
    {
        _config = config;
        _modelManager = modelManager;
        _systemIdleTracker = systemIdleTracker;
        _dashboardService = dashboardService;
        _forwarder = forwarder;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RouteAsync(HttpContext context, CancellationToken ct)
    {
        var request = context.Request;
        var modelName = request.Headers[ProxyHeaders.Model].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(modelName))
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("MissingHeader",
                $"The 'X-Llama-Model' header is required. Available models: {string.Join(", ", _config.Value.Models.Keys)}"));
            return;
        }

        var modelConfig = _config.Value.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("UnknownModel",
                $"Model '{modelName}' not found. Available models: {string.Join(", ", _config.Value.Models.Keys)}"));
            return;
        }

        var launchParams = LoadParamsForModel(modelName);

        await WarnIfModelAlreadyRunningAsync(launchParams, modelName, context.RequestAborted);
        if (context.Response.HasStarted) return;

        try
        {
            await _modelManager.EnsureModelAsync(modelName, launchParams, context.RequestAborted);
        }
        catch (Exception ex)
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.ServiceUnavailable(ex.Message));
            return;
        }

        _systemIdleTracker.RecordRequest();

        var backendUrl = modelConfig.BackendUrl ?? _config.Value.BackendUrl;
        var targetUrl = RequestForwarder.BuildTargetUrl(backendUrl, request);

        await _forwarder.ForwardAsync(context, targetUrl, ct);
    }

    private ModelLaunchParams LoadParamsForModel(string modelName)
    {
        try
        {
            return _dashboardService.LoadParams(modelName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load params for model '{Model}', using defaults", modelName);
            return ModelLaunchParams.Defaults;
        }
    }

    // TODO: add tests for /v1/{**path} forwarding with path stripping
    private async Task WarnIfModelAlreadyRunningAsync(ModelLaunchParams? launchParams, string modelName, CancellationToken ct = default)
    {
        if (launchParams is not null)
        {
            var activeModel = await _modelManager.GetActiveModelNameAsync(ct);
            if (activeModel == modelName)
            {
                _logger.LogWarning(
                    "Model '{Model}' is already running — header launch params will be ignored (only the first start uses them)",
                    modelName);
            }
        }
    }


}
