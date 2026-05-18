using LlaModem.Config;
using LlaModem.Models;
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
        endpoints.MapDashboardEndpoints();
    }

    public static void MapStatsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var statsGroup = endpoints.MapGroup("/admin/stats");

        statsGroup.MapGet("/usage", async (IStatsService stats, int days = 30, string? model = null) =>
        {
            days = Math.Clamp(days, 1, 365);
            var response = await stats.GetDailyUsageAsync(days, model);
            return Results.Json(response);
        }).WithName("GetDailyUsage");

        statsGroup.MapGet("/requests", async (IStatsService stats, int limit = 50, int offset = 0, string? model = null) =>
        {
            limit = Math.Clamp(limit, 1, 500);
            offset = Math.Max(offset, 0);
            var response = await stats.GetRecentRequestsAsync(limit, offset, model);
            return Results.Json(response);
        }).WithName("GetRecentRequests");

        statsGroup.MapGet("/cost-comparison", async (IStatsService stats, int days = 30, string? model = null) =>
        {
            days = Math.Clamp(days, 1, 365);
            var usage = await stats.GetDailyUsageAsync(days, model);
            var costs = ModelPricing.All
                .Select(p => new ModelCost(
                    p.Name,
                    ModelPricing.CalculateCost(p, usage.Summary.TotalPromptTokens, 0),
                    ModelPricing.CalculateCost(p, 0, usage.Summary.TotalCompletionTokens),
                    ModelPricing.CalculateCost(p, usage.Summary.TotalPromptTokens, usage.Summary.TotalCompletionTokens)))
                .OrderBy(c => c.TotalCost)
                .ToArray();

            var cheapest = costs.First();
            var mostExpensive = costs.Last();

            return Results.Json(new CostComparisonResponse(
                usage.Period,
                model,
                costs,
                cheapest,
                mostExpensive));
        }).WithName("GetCostComparison");
    }

    private static void MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (IMetaModelManager modelManager, CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
            return Results.Json(new { status = "ok", activeModel });
        });
    }

    private static void MapV1ProxyEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map("/v1/{**path}", async (HttpContext context, IModelProxyHandler handler, CancellationToken ct) =>
        {
            await handler.RouteAsync(context, ct);
        });
    }

     public static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin");

        adminGroup.MapGet("/status", async (IMetaModelManager modelManager, IOptions<AppConfig> config, CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
            return Results.Json(new { activeModel, backendUrl = config.Value.BackendUrl });
        });

        adminGroup.MapPost("/model", async (HttpContext context, IMetaModelManager modelManager) =>
        {
            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<SwitchModelRequest>(context.Request.Body);
            if (body?.Model == null)
            {
                await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("BadRequest",
                    "Provide a 'model' field in the request body."));
                return;
            }

            try
            {
                await modelManager.EnsureModelAsync(body.Model, null, context.RequestAborted);
                var activeModel = await modelManager.GetActiveModelNameAsync(context.RequestAborted);
                await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.Ok(new { activeModel }));
            }
            catch (Exception ex)
            {
                await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.ServiceUnavailable(ex.Message));
            }
        });

        adminGroup.MapPost("/stop", async (HttpContext context, IMetaModelManager modelManager) =>
        {
            var stopped = await modelManager.GetActiveModelNameAsync(context.RequestAborted);
            if (stopped is null)
                return Results.Json(new { message = "No active model to stop." }, statusCode: 400);

            await modelManager.StopActiveModelAsync(context.RequestAborted);
            return Results.Json(new { message = $"Model '{stopped}' stopped." });
        });
    }
}

public record SwitchModelRequest(string? Model);

public static class DashboardEndpointExtensions
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var dashboardGroup = endpoints.MapGroup("/admin/dashboard");

        dashboardGroup.MapGet("/models", async (DashboardService dashboard, CancellationToken ct) =>
        {
            var models = await dashboard.GetAllModelsAsync(ct);
            return Results.Json(models);
        }).WithName("GetAllModels");

        dashboardGroup.MapGet("/models/{name}", async (
            DashboardService dashboard,
            string name,
            CancellationToken ct) =>
        {
            var model = await dashboard.GetModelAsync(name, ct);
            if (model is null)
            {
                return Results.NotFound(new { error = "ModelNotFound", message = $"Model '{name}' not found" });
            }
            return Results.Json(model);
        }).WithName("GetModel");

        dashboardGroup.MapPost("/models/{name}/start", async (
            HttpContext context,
            IMetaModelManager modelManager,
            DashboardService dashboard,
            string name,
            CancellationToken ct) =>
        {
            StartModelRequest? body;
            try
            {
                body = await System.Text.Json.JsonSerializer.DeserializeAsync<StartModelRequest>(context.Request.Body);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "InvalidRequest", message = "Request body must be valid JSON" });
            }

            if (body is null)
            {
                return Results.BadRequest(new { error = "InvalidRequest", message = "Request body is required" });
            }

            var launchParams = body.ToLaunchParams();
            try
            {
                await modelManager.EnsureModelAsync(name, launchParams, ct);
                var state = await dashboard.GetModelStateAsync(name, ct);
                return Results.Ok(new
                {
                    message = "Model started successfully",
                    processId = state?.ProcessId,
                    startedAt = state?.StartedAt.ToString("o")
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "StartFailed", message = ex.Message }, statusCode: 503);
            }
        }).WithName("StartModel");

        dashboardGroup.MapPost("/models/{name}/stop", async (
            IMetaModelManager modelManager,
            string name,
            CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
            if (activeModel is null)
            {
                return Results.NotFound(new { error = "ModelNotFound", message = $"No model is currently active" });
            }

            if (activeModel != name)
            {
                return Results.BadRequest(new { error = "ModelNotActive", message = $"Model '{activeModel}' is active, not '{name}'" });
            }

            try
            {
                await modelManager.StopActiveModelAsync(ct);
                return Results.Ok(new { message = $"Model '{name}' stopped" });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "StopFailed", message = ex.Message }, statusCode: 503);
            }
        }).WithName("StopModel");

        dashboardGroup.MapPut("/models/{name}/params", async (
            DashboardService dashboard,
            HttpContext context,
            string name,
            CancellationToken ct) =>
        {
            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<UpdateModelParamsRequest>(context.Request.Body);
            if (body is null)
            {
                return Results.BadRequest(new { error = "InvalidRequest", message = "Request body is required" });
            }

            try
            {
                var result = dashboard.UpdateParamsAsync(name, body);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = "ModelNotFound", message = ex.Message });
            }
        }).WithName("UpdateModelParams");

        dashboardGroup.MapGet("/models/{name}/health", async (
            IMetaModelManager modelManager,
            HealthChecker healthChecker,
            IOptions<AppConfig> config,
            string name,
            CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
            if (activeModel != name)
            {
                return Results.BadRequest(new { error = "ModelNotRunning", message = $"Model '{name}' is not currently active" });
            }

            var healthUrl = $"{config.Value.BackendUrl.TrimEnd('/')}/health";
            var startTime = DateTimeOffset.UtcNow;
            var (success, _) = await healthChecker.CheckAsync(healthUrl, ct);
            var responseTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            return Results.Ok(new
            {
                IsHealthy = success,
                HealthUrl = healthUrl,
                LastCheckedAt = DateTimeOffset.UtcNow.ToString("o"),
                ResponseTimeMs = responseTime
            });
        }).WithName("GetModelHealth");
    }
}
