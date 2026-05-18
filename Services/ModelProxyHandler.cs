using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public sealed class ModelProxyHandler : IModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly IMetaModelManager _modelManager;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly IIdleTimeoutResetter? _idleTimeoutResetter;

    private readonly LaunchParamParser _paramParser;
    private readonly IRequestForwarder _forwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        IMetaModelManager modelManager,
        SystemIdleTracker systemIdleTracker,
        IIdleTimeoutResetter? idleTimeoutResetter,
        LaunchParamParser paramParser,
        IRequestForwarder forwarder,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelProxyHandler> logger)
    {
        _config = config;
        _modelManager = modelManager;
        _systemIdleTracker = systemIdleTracker;
        _idleTimeoutResetter = idleTimeoutResetter;
        _paramParser = paramParser;
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

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/v1", StringComparison.Ordinal) && !path.StartsWith("/admin", StringComparison.Ordinal))
        {
            _idleTimeoutResetter?.Reset();
        }

        _systemIdleTracker.RecordRequest();

        var backendUrl = modelConfig.BackendUrl ?? _config.Value.BackendUrl;
        var targetUrl = RequestForwarder.BuildTargetUrl(backendUrl, request);

        await _forwarder.ForwardAsync(context, targetUrl, ct);
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
