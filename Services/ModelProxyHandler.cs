using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class ModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly ModelManager _modelManager;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly LaunchParamParser _paramParser;
    private readonly RequestForwarder _forwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        ModelManager modelManager,
        SystemIdleTracker systemIdleTracker,
        LaunchParamParser paramParser,
        RequestForwarder forwarder,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelProxyHandler> logger)
    {
        _config = config;
        _modelManager = modelManager;
        _systemIdleTracker = systemIdleTracker;
        _paramParser = paramParser;
        _forwarder = forwarder;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProxyAsync(HttpContext context, HttpRequest request)
    {
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

        var launchParams = await _paramParser.ParseAsync(context, request);
        if (launchParams is null && context.Response.HasStarted) return; // error was written

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

        var targetUrl = RequestForwarder.BuildTargetUrl(_config.Value.BackendUrl, request);

        using var httpClient = _httpClientFactory.CreateClient("ModelManager");

        await _forwarder.ForwardAsync(context, request, httpClient, targetUrl);
    }

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
