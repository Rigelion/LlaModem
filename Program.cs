using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.Extensions.Options;

namespace LlaModem;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind configuration
        builder.Services.Configure<AppConfig>(builder.Configuration);
        builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();

        builder.Services.Configure<RouterConfig>(builder.Configuration.GetSection("Router"));
        builder.Services.AddOptions<RouterConfig>().Bind(builder.Configuration.GetSection("Router")).ValidateOnStart();

        // Configure Kestrel to listen on the configured URL
        var routerConfig = builder.Configuration.GetSection("Router");
        var listenUrl = routerConfig["ListenUrl"] ?? "http://localhost:9000";
        var uri = new Uri(listenUrl);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.ListenAnyIP(uri.Port);
        });

        // Register services
        builder.Services.AddSingleton<IModelLauncher, DefaultModelLauncher>();
        builder.Services.AddSingleton<ModelManager>();
        builder.Services.AddSingleton<IRequestTracker, RequestTracker>();
        builder.Services.AddHostedService<IdleTimeoutService>();

        var app = builder.Build();

        // Health endpoint (unauthenticated)
        app.MapGet("/health", (ModelManager modelManager) =>
        {
            var activeModel = modelManager.ActiveModelName;
            return Results.Json(new { status = "ok", activeModel });
        });

        // Apply Basic Auth to /v1/* routes
        app.UseBasicAuthWhen("/v1");

        // v1 proxy route
        app.Map("/v1/**", async (
            HttpContext context,
            HttpRequest request,
            ModelManager modelManager,
            IRequestTracker requestTracker,
            IOptions<AppConfig> config,
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

            try
            {
                await modelManager.EnsureModelAsync(modelName);
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

        // Admin endpoints
        var adminGroup = app.MapGroup("/admin");

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

        app.Run();
    }
}

public record SwitchModelRequest(string? Model);
