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
        endpoints.MapGet("/health", async (ModelManager modelManager, CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
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

        adminGroup.MapGet("/status", async (ModelManager modelManager, IOptions<AppConfig> config, CancellationToken ct) =>
        {
            var activeModel = await modelManager.GetActiveModelNameAsync(ct);
            return Results.Json(new { activeModel, backendUrl = config.Value.BackendUrl });
        });

        adminGroup.MapPost("/model", async (HttpContext context, ModelManager modelManager) =>
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

        adminGroup.MapPost("/stop", async (HttpContext context, ModelManager modelManager) =>
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
