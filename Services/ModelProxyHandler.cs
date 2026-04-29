using LlaModem.Config;
using LlaModem.Utilities;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class ModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly ModelManager _modelManager;
    private readonly IRequestTracker _requestTracker;
    private readonly IHeaderValueInjector _headerValueInjector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        ModelManager modelManager,
        IRequestTracker requestTracker,
        IHeaderValueInjector headerValueInjector,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelProxyHandler> logger)
    {
        _config = config;
        _modelManager = modelManager;
        _requestTracker = requestTracker;
        _headerValueInjector = headerValueInjector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProxyAsync(HttpContext context, HttpRequest request)
    {
        var modelName = request.Headers["X-Llama-Model"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(modelName))
        {
            await WriteErrorAsync(context, 400, "Missing header",
                $"The 'X-Llama-Model' header is required. Available models: {string.Join(", ", _config.Value.Models.Keys)}");
            return;
        }

        var modelConfig = _config.Value.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            await WriteErrorAsync(context, 400, "Unknown model",
                $"Model '{modelName}' not found. Available models: {string.Join(", ", _config.Value.Models.Keys)}");
            return;
        }

        var launchParams = await ParseLaunchParamsAsync(context, request);
        if (launchParams is null && context.Response.HasStarted) return; // error was written

        WarnIfModelAlreadyRunning(launchParams, modelName);
        if (context.Response.HasStarted) return;

        try
        {
            await _modelManager.EnsureModelAsync(modelName, launchParams);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, 503, "Model unavailable", ex.Message);
            return;
        }

        _requestTracker.RecordRequest();

        var targetUrl = BuildTargetUrl(modelConfig, request);

        using var httpClient = _httpClientFactory.CreateClient("ModelManager");

        await ForwardRequestAsync(context, request, httpClient, targetUrl);
    }

    private async Task<ModelLaunchParams?> ParseLaunchParamsAsync(HttpContext context, HttpRequest request)
    {
        double? temperature = null;
        double? topP = null;
        double? presencePenalty = null;
        bool hasLaunchParams = false;

        var tempResult = await TryParseDoubleHeader(context, request, "X-Llama-Temperature");
        if (tempResult.Parsed.HasValue) { temperature = tempResult.Parsed.Value; hasLaunchParams = true; }
        else if (tempResult.Error) return null;

        var topPResult = await TryParseDoubleHeader(context, request, "X-Llama-TopP");
        if (topPResult.Parsed.HasValue) { topP = topPResult.Parsed.Value; hasLaunchParams = true; }
        else if (topPResult.Error) return null;

        var ppResult = await TryParseDoubleHeader(context, request, "X-Llama-PresencePenalty");
        if (ppResult.Parsed.HasValue) { presencePenalty = ppResult.Parsed.Value; hasLaunchParams = true; }
        else if (ppResult.Error) return null;

        return hasLaunchParams ? new ModelLaunchParams(temperature, topP, presencePenalty) : null;
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

    private static string BuildTargetUrl(ModelConfig modelConfig, HttpRequest request)
    {
        var backendUrl = modelConfig.BackendUrl.TrimEnd('/');
        var path = request.Path.Value!;
        var targetPath = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ? path[3..] : path;
        var targetUrl = $"{backendUrl}{targetPath}";
        if (request.QueryString.HasValue)
            targetUrl += request.QueryString.Value;
        return targetUrl;
    }

    private static async Task ForwardRequestAsync(
        HttpContext context,
        HttpRequest request,
        HttpClient httpClient,
        string targetUrl)
    {
        // Inject configured header values into the JSON request body
        await context.RequestServices.GetRequiredService<IHeaderValueInjector>()
            .InjectAsync(context, context.RequestServices.GetRequiredService<ILogger<ModelProxyHandler>>());

        // Explicitly capture the (possibly modified) body so forwarding is independent of middleware ordering
        var buffer = await HttpRequestExtensions.ReadBodyAsync(request);

        var method = System.Net.Http.HttpMethod.Parse(request.Method);
        var forwardedRequest = new HttpRequestMessage(method, targetUrl);

        foreach (var header in request.Headers)
        {
            if (EndpointSetup.ExcludedHeaders.Contains(header.Key))
                continue;
            forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        }

        if (buffer.Length > 0)
        {
            forwardedRequest.Content = new ByteArrayContent(buffer);
            foreach (var header in request.Headers)
            {
                if (header.Key is "Content-Length")
                    continue;
                if (EndpointSetup.ExcludedHeaders.Contains(header.Key))
                    continue;
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    forwardedRequest.Content!.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
                }
            }
        }

        var response = await httpClient.SendAsync(
            forwardedRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        foreach (var header in response.Headers)
        {
            if (header.Key is "Transfer-Encoding")
                continue;
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.StatusCode = (int)response.StatusCode;
        await response.Content.CopyToAsync(context.Response.Body);
    }



    private static async Task<(double? Parsed, bool Error)> TryParseDoubleHeader(
        HttpContext context, HttpRequest request, string headerName)
    {
        var headerValue = request.Headers[headerName].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return (null, false);

        if (!double.TryParse(headerValue, out var parsed))
        {
            await WriteErrorAsync(context, 400, "Bad request",
                $"Invalid {headerName} value: '{headerValue}'");
            return (null, true);
        }

        return (parsed, false);
    }

    private static async Task WriteErrorAsync(
        HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error, message });
    }
}
