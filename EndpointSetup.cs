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
        endpoints.Map("/v1/{**path}", async (HttpContext context, HttpRequest request, ModelProxyHandler handler) =>
        {
            await handler.ProxyAsync(context, request);
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
