using System.Collections.Generic;
using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.Extensions.Options;

namespace LlaModem;

public static class EndpointSetup
{
    private static readonly HashSet<string> ExcludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade"
    };

    public static void ConfigureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthEndpoint();
        endpoints.MapV1ProxyEndpoint();
        endpoints.MapAdminEndpoints();
    }

    private static void MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", (ModelManager modelManager) =>
        {
            var activeModel = modelManager.ActiveModelName;
            return Results.Json(new { status = "ok", activeModel });
        });
    }

    private static void MapV1ProxyEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map("/v1/{**path}", async (
            HttpContext context,
            HttpRequest request,
            ModelManager modelManager,
            IRequestTracker requestTracker,
            IOptions<AppConfig> config,
            IHeaderValueInjector headerValueInjector,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger) =>
        {
            var modelName = request.Headers["X-Llama-Model"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(modelName))
            {
                var available = string.Join(", ", config.Value.Models.Keys);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Missing header", message = $"The 'X-Llama-Model' header is required. Available models: {available}" });
                return;
            }

            var modelConfig = config.Value.Models.GetValueOrDefault(modelName);
            if (modelConfig is null)
            {
                var available = string.Join(", ", config.Value.Models.Keys);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Unknown model", message = $"Model '{modelName}' not found. Available models: {available}" });
                return;
            }

            // Extract optional launch params from headers
            double? temperature = null;
            double? topP = null;
            double? presencePenalty = null;
            bool hasLaunchParams = false;

            var tempResult = await TryParseDoubleHeader(context, request, "X-Llama-Temperature");
            if (tempResult.Parsed.HasValue) { temperature = tempResult.Parsed.Value; hasLaunchParams = true; }
            else if (tempResult.Error) return;

            var topPResult = await TryParseDoubleHeader(context, request, "X-Llama-TopP");
            if (topPResult.Parsed.HasValue) { topP = topPResult.Parsed.Value; hasLaunchParams = true; }
            else if (topPResult.Error) return;

            var ppResult = await TryParseDoubleHeader(context, request, "X-Llama-PresencePenalty");
            if (ppResult.Parsed.HasValue) { presencePenalty = ppResult.Parsed.Value; hasLaunchParams = true; }
            else if (ppResult.Error) return;

            var launchParams = hasLaunchParams ? new ModelLaunchParams(temperature, topP, presencePenalty) : null;

            // Warn if model is already running but different header values were provided
            if (launchParams is not null && modelManager.ActiveModelName == modelName)
            {
                logger.LogWarning(
                    "Model '{Model}' is already running — header launch params will be ignored (only the first start uses them)",
                    modelName);
            }

            try
            {
                await modelManager.EnsureModelAsync(modelName, launchParams);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "Model unavailable", message = ex.Message });
                return;
            }

            requestTracker.RecordRequest();

            var backendUrl = modelConfig.BackendUrl.TrimEnd('/');
            var path = request.Path.Value!;
            var targetPath = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ? path[3..] : path;
            var targetUrl = $"{backendUrl}{targetPath}";
            if (request.QueryString.HasValue)
                targetUrl += request.QueryString.Value;

            // Inject configured header values into the JSON request body
            await headerValueInjector.InjectAsync(context, logger);

            using var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            try
            {
                var method = System.Net.Http.HttpMethod.Parse(request.Method);
                var forwardedRequest = new HttpRequestMessage(method, targetUrl);

                foreach (var header in request.Headers)
                {
                    if (ExcludedHeaders.Contains(header.Key))
                        continue;
                    forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
                }

                if (request.Body != null && request.Body.CanRead)
                {
                    forwardedRequest.Content = new StreamContent(request.Body);
                    foreach (var header in request.Headers)
                    {
                        if (header.Key is "Content-Length")
                            continue;
                        if (ExcludedHeaders.Contains(header.Key))
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

                // Note: Transfer-Encoding is excluded from both request forwarding and response copying.
                context.Response.StatusCode = (int)response.StatusCode;
                await response.Content.CopyToAsync(context.Response.Body);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "Backend error", message = $"Failed to reach backend: {ex.Message}" });
            }
        });
    }

    /// <summary>
    /// Parses a double-valued header. Returns a tuple of (Value, Error).
    /// Value is null when the header is absent (no error). Error is true when the header is present but invalid.
    /// </summary>
    private static async Task<(double? Parsed, bool Error)> TryParseDoubleHeader(HttpContext context, HttpRequest request, string headerName)
    {
        var headerValue = request.Headers[headerName].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return (null, false);

        if (!double.TryParse(headerValue, out var parsed))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = $"Invalid {headerName} value: '{headerValue}'" });
            return (null, true);
        }

        return (parsed, false);
    }

    private static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin");

        adminGroup.MapGet("/status", (ModelManager modelManager, IOptions<AppConfig> config) =>
        {
            var activeModel = modelManager.ActiveModelName;
            string? backendUrl = null;
            if (activeModel != null && config.Value.Models.TryGetValue(activeModel, out var mc))
                backendUrl = mc.BackendUrl;
            return Results.Json(new { activeModel, backendUrl });
        });

        adminGroup.MapPost("/model", async (HttpContext context, ModelManager modelManager) =>
        {
            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<SwitchModelRequest>(context.Request.Body);
            if (body?.Model == null)
                return Results.Json(new { error = "Bad request", message = "Provide a 'model' field in the request body." }, statusCode: 400);

            try
            {
                await modelManager.EnsureModelAsync(body.Model);
                return Results.Json(new { activeModel = modelManager.ActiveModelName });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "Model unavailable", message = ex.Message }, statusCode: 503);
            }
        });

        adminGroup.MapPost("/stop", async (HttpContext context, ModelManager modelManager) =>
        {
            var stopped = modelManager.ActiveModelName;
            if (modelManager.ActiveModelName == null)
                return Results.Json(new { message = "No active model to stop." }, statusCode: 400);

            await modelManager.StopActiveModelAsync();
            return Results.Json(new { message = $"Model '{stopped}' stopped." });
        });
    }
}

public record SwitchModelRequest(string? Model);
