using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class ModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly ModelManager _modelManager;
    private readonly ISystemIdleTracker _systemIdleTracker;
    private readonly ILaunchParamParser _paramParser;
    private readonly IRequestForwarder _forwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        ModelManager modelManager,
        ISystemIdleTracker systemIdleTracker,
        ILaunchParamParser paramParser,
        IRequestForwarder forwarder,
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
            await ErrorResponseWriter.WriteAsync(context, 400, "Missing header",
                $"The 'X-Llama-Model' header is required. Available models: {string.Join(", ", _config.Value.Models.Keys)}");
            return;
        }

        var modelConfig = _config.Value.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            await ErrorResponseWriter.WriteAsync(context, 400, "Unknown model",
                $"Model '{modelName}' not found. Available models: {string.Join(", ", _config.Value.Models.Keys)}");
            return;
        }

        var launchParams = await _paramParser.ParseAsync(context, request);
        if (launchParams is null && context.Response.HasStarted) return; // error was written

        WarnIfModelAlreadyRunning(launchParams, modelName);
        if (context.Response.HasStarted) return;

        try
        {
            await _modelManager.EnsureModelAsync(modelName, launchParams);
        }
        catch (Exception ex)
        {
            await ErrorResponseWriter.WriteAsync(context, 503, "Model unavailable", ex.Message);
            return;
        }

        _systemIdleTracker.RecordRequest();

        var targetUrl = RequestForwarder.BuildTargetUrl(_config.Value.BackendUrl, request);

        using var httpClient = _httpClientFactory.CreateClient("ModelManager");

        await _forwarder.ForwardAsync(context, request, httpClient, targetUrl);
    }

    private void WarnIfModelAlreadyRunning(ModelLaunchParams? launchParams, string modelName)
    {
        if (launchParams is not null && _modelManager.ActiveModelName == modelName)
        {
            _logger.LogWarning(
                "Model '{Model}' is already running — header launch params will be ignored (only the first start uses them)",
                modelName);
        }
    }


}
