using LlaModem.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LlaModem.Services;

public class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        var dateRangeSchema = context.GetOrCreateSchemaAsync(typeof(DateRange), null, cancellationToken).Result;
        if (dateRangeSchema != null) document.Components.Schemas["DateRange"] = dateRangeSchema;

        var modelDashboardItemSchema = context.GetOrCreateSchemaAsync(typeof(ModelDashboardItem), null, cancellationToken).Result;
        if (modelDashboardItemSchema != null) document.Components.Schemas["ModelDashboardItem"] = modelDashboardItemSchema;

        var startModelRequestSchema = context.GetOrCreateSchemaAsync(typeof(StartModelRequest), null, cancellationToken).Result;
        if (startModelRequestSchema != null) document.Components.Schemas["StartModelRequest"] = startModelRequestSchema;

        var updateModelParamsRequestSchema = context.GetOrCreateSchemaAsync(typeof(UpdateModelParamsRequest), null, cancellationToken).Result;
        if (updateModelParamsRequestSchema != null) document.Components.Schemas["UpdateModelParamsRequest"] = updateModelParamsRequestSchema;

        var healthCheckResponseSchema = context.GetOrCreateSchemaAsync(typeof(HealthCheckResponse), null, cancellationToken).Result;
        if (healthCheckResponseSchema != null) document.Components.Schemas["HealthCheckResponse"] = healthCheckResponseSchema;

        var parameterUpdateResponseSchema = context.GetOrCreateSchemaAsync(typeof(ParameterUpdateResponse), null, cancellationToken).Result;
        if (parameterUpdateResponseSchema != null) document.Components.Schemas["ParameterUpdateResponse"] = parameterUpdateResponseSchema;

        var dailyUsageResponseSchema = context.GetOrCreateSchemaAsync(typeof(DailyUsageResponse), null, cancellationToken).Result;
        if (dailyUsageResponseSchema != null) document.Components.Schemas["DailyUsageResponse"] = dailyUsageResponseSchema;

        var recentRequestsResponseSchema = context.GetOrCreateSchemaAsync(typeof(RecentRequestsResponse), null, cancellationToken).Result;
        if (recentRequestsResponseSchema != null) document.Components.Schemas["RecentRequestsResponse"] = recentRequestsResponseSchema;

        var costComparisonResponseSchema = context.GetOrCreateSchemaAsync(typeof(CostComparisonResponse), null, cancellationToken).Result;
        if (costComparisonResponseSchema != null) document.Components.Schemas["CostComparisonResponse"] = costComparisonResponseSchema;

        return Task.CompletedTask;
    }
}
