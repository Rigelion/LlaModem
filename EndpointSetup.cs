using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.Extensions.Options;

namespace LlaModem;

public static class EndpointSetup
{
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

            var tempHeader = request.Headers["X-Llama-Temperature"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tempHeader) && double.TryParse(tempHeader, out var tempVal))
            {
                temperature = tempVal;
                hasLaunchParams = true;
            }
            else if (!string.IsNullOrEmpty(tempHeader))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = $"Invalid X-Llama-Temperature value: '{tempHeader}'" });
                return;
            }

            var topPHeader = request.Headers["X-Llama-TopP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(topPHeader) && double.TryParse(topPHeader, out var topPVal))
            {
                topP = topPVal;
                hasLaunchParams = true;
            }
            else if (!string.IsNullOrEmpty(topPHeader))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = $"Invalid X-Llama-TopP value: '{topPHeader}'" });
                return;
            }

            var ppHeader = request.Headers["X-Llama-PresencePenalty"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ppHeader) && double.TryParse(ppHeader, out var ppVal))
            {
                presencePenalty = ppVal;
                hasLaunchParams = true;
            }
            else if (!string.IsNullOrEmpty(ppHeader))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = $"Invalid X-Llama-PresencePenalty value: '{ppHeader}'" });
                return;
            }

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

            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            // Inject configured header values into the JSON request body
            headerValueInjector.Inject(context, logger);

            try
            {
                var method = System.Net.Http.HttpMethod.Parse(request.Method);
                var forwardedRequest = new HttpRequestMessage(method, targetUrl);

                foreach (var header in request.Headers)
                {
                    if (header.Key is "Host" or "Connection" or "Keep-Alive" or "Transfer-Encoding" or "Upgrade")
                        continue;
                    forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
                }

                if (request.Body != null && request.Body.CanRead)
                {
                    forwardedRequest.Content = new StreamContent(request.Body);
                    foreach (var header in request.Headers)
                    {
                        if (header.Key is "Host" or "Connection" or "Keep-Alive" or "Transfer-Encoding" or "Upgrade" or "Content-Length")
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
            catch (Exception ex)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "Backend error", message = $"Failed to reach backend: {ex.Message}" });
            }
            finally
            {
                httpClient.Dispose();
            }
        });
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
            {
                await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = "Provide a 'model' field in the request body." });
                return 400;
            }

            try
            {
                await modelManager.EnsureModelAsync(body.Model);
                await context.Response.WriteAsJsonAsync(new { activeModel = modelManager.ActiveModelName });
                return 200;
            }
            catch (Exception ex)
            {
                await context.Response.WriteAsJsonAsync(new { error = "Model unavailable", message = ex.Message });
                return 503;
            }
        });

        adminGroup.MapPost("/stop", async (HttpContext context, ModelManager modelManager) =>
        {
            var stopped = modelManager.ActiveModelName;
            if (modelManager.ActiveModelName == null)
            {
                await context.Response.WriteAsJsonAsync(new { message = "No active model to stop." });
                return 400;
            }

            await modelManager.StopActiveModelAsync();
            await context.Response.WriteAsJsonAsync(new { message = $"Model '{stopped}' stopped." });
            return 200;
        });
    }
}

public record SwitchModelRequest(string? Model);
