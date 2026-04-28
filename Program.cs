using LlamaDem.Config;
using LlamaDem.Middleware;
using LlamaDem.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration
builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();

builder.Services.Configure<RouterConfig>(builder.Configuration.GetSection("Router"));
builder.Services.AddOptions<RouterConfig>().Bind(builder.Configuration.GetSection("Router")).ValidateOnStart();

// Register services
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

// v1 proxy route — forwards to the model selected by X-Llama-Model header
app.Map("/v1/**", async (
    HttpContext context,
    HttpRequest request,
    ModelManager modelManager,
    IRequestTracker requestTracker,
    IOptions<AppConfig> config,
    ILogger<Program> logger) =>
{
    // 1. Read model from header
    var modelName = request.Headers["X-Llama-Model"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(modelName))
    {
        var available = string.Join(", ", config.Value.Models.Keys);
        logger.LogWarning("Request to {Path} missing X-Llama-Model header. Available: {Available}",
            request.Path, available);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Missing header", message = $"The 'X-Llama-Model' header is required. Available models: {available}" });
        return;
    }

    var modelConfig = config.Value.Models.GetValueOrDefault(modelName);
    if (modelConfig is null)
    {
        var available = string.Join(", ", config.Value.Models.Keys);
        logger.LogWarning("Unknown model '{Model}' requested on {Path}. Available: {Available}",
            modelName, request.Path, available);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Unknown model", message = $"Model '{modelName}' not found. Available models: {available}" });
        return;
    }

    // 2. Ensure model is running
    try
    {
        await modelManager.EnsureModelAsync(modelName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure model '{Model}' is running", modelName);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Model unavailable", message = $"Failed to start/switch to model '{modelName}': {ex.Message}" });
        return;
    }

    // 3. Track request for idle timeout
    requestTracker.RecordRequest();

    // 4. Forward to backend
    var backendUrl = modelConfig.BackendUrl.TrimEnd('/');
    var path = request.Path.Value!;
    var v1Prefix = "/v1";
    var targetPath = path.StartsWith(v1Prefix, StringComparison.OrdinalIgnoreCase)
        ? path[v1Prefix.Length..]
        : path;
    var targetUrl = $"{backendUrl}{targetPath}";
    if (request.QueryString.HasValue)
        targetUrl += request.QueryString.Value;

    logger.LogDebug("Forwarding {Method} {Path} → {TargetUrl} (model: {Model})",
        request.Method, request.Path, targetUrl, modelName);

    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

    try
    {
        var method = System.Net.Http.HttpMethod.Parse(request.Method);
        var forwardedRequest = new HttpRequestMessage(method, targetUrl);

        // Copy headers (exclude hop-by-hop)
        foreach (var header in request.Headers)
        {
            if (header.Key is "Host" or "Connection" or "Keep-Alive" or "Transfer-Encoding" or "Upgrade")
                continue;
            forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        }

        // Copy body if present
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

        // Copy response headers and status code
        foreach (var header in response.Headers)
        {
            if (header.Key is "Transfer-Encoding")
                continue;
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        context.Response.StatusCode = (int)response.StatusCode;

        // Stream response body back to client
        await response.Content.CopyToAsync(context.Response.Body);

        logger.LogInformation(
            "{Method} {Path} → {StatusCode} (model: {Model}, backend: {Backend})",
            request.Method, request.Path, response.StatusCode, modelName, backendUrl);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to forward request to backend for model '{Model}'", modelName);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Backend error", message = $"Failed to reach backend: {ex.Message}" });
    }
    finally
    {
        httpClient.Dispose();
    }
});

// Admin endpoints (unauthenticated)
var adminGroup = app.MapGroup("/admin");

// GET /admin/status
adminGroup.MapGet("/status", (ModelManager modelManager, IOptions<AppConfig> config) =>
{
    var activeModel = modelManager.ActiveModelName;
    string? backendUrl = null;
    if (activeModel != null && config.Value.Models.TryGetValue(activeModel, out var mc))
    {
        backendUrl = mc.BackendUrl;
    }
    return Results.Json(new { activeModel, backendUrl });
});

// POST /admin/model — switch model
adminGroup.MapPost("/model", async (HttpContext context, ModelManager modelManager) =>
{
    var body = await System.Text.Json.JsonSerializer.DeserializeAsync<SwitchModelRequest>(context.Request.Body);
    if (body?.Model == null)
    {
        await context.Response.WriteAsJsonAsync(new { error = "Bad request", message = "Provide a 'model' field in the request body." });
        return StatusCodes.Status400BadRequest;
    }

    try
    {
        await modelManager.EnsureModelAsync(body.Model);
        await context.Response.WriteAsJsonAsync(new { activeModel = modelManager.ActiveModelName });
        return StatusCodes.Status200OK;
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsJsonAsync(new { error = "Model unavailable", message = ex.Message });
        return StatusCodes.Status503ServiceUnavailable;
    }
});

// POST /admin/stop — stop active model
adminGroup.MapPost("/stop", async (HttpContext context, ModelManager modelManager) =>
{
    if (modelManager.ActiveModelName == null)
    {
        await context.Response.WriteAsJsonAsync(new { message = "No active model to stop." });
        return StatusCodes.Status400BadRequest;
    }

    await modelManager.StopActiveModelAsync();
    await context.Response.WriteAsJsonAsync(new { message = $"Model '{modelManager.ActiveModelName ?? "(none)"}' stopped." });
    // Note: ActiveModelName is null after stop, so we captured it above
    return StatusCodes.Status200OK;
});

app.Run();

public record SwitchModelRequest(string? Model);
