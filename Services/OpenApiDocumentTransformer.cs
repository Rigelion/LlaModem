using LlaModem.Models;
using LlaModem.Services.OpenApiExtensions;
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

        // Register all model schemas
        RegisterSchema(context, typeof(DateRange), "DateRange", document);
        RegisterSchema(context, typeof(ModelDashboardItem), "ModelDashboardItem", document);
        RegisterSchema(context, typeof(StartModelRequest), "StartModelRequest", document);
        RegisterSchema(context, typeof(UpdateModelParamsRequest), "UpdateModelParamsRequest", document);
        RegisterSchema(context, typeof(HealthCheckResponse), "HealthCheckResponse", document);
        RegisterSchema(context, typeof(ParameterUpdateResponse), "ParameterUpdateResponse", document);
        RegisterSchema(context, typeof(DailyUsageResponse), "DailyUsageResponse", document);
        RegisterSchema(context, typeof(RecentRequestsResponse), "RecentRequestsResponse", document);
        RegisterSchema(context, typeof(CostComparisonResponse), "CostComparisonResponse", document);

        // Map endpoint schemas
        MapGetDailyUsageEndpoint(document);
        MapGetRecentRequestsEndpoint(document);
        MapGetCostComparisonEndpoint(document);
        MapGetAllModelsEndpoint(document);
        MapGetModelEndpoint(document);
        MapStartModelEndpoint(document);
        MapStopModelEndpoint(document);
        MapUpdateModelParamsEndpoint(document);
        MapGetModelHealthEndpoint(document);
        MapGetStatusEndpoint(document);
        MapPostModelEndpoint(document);
        MapPostStopEndpoint(document);

        return Task.CompletedTask;
    }

    private static void RegisterSchema(
        OpenApiDocumentTransformerContext context,
        Type type,
        string name,
        OpenApiDocument document)
    {
        var schema = context.GetOrCreateSchemaAsync(type, null, CancellationToken.None).Result;
        if (schema != null && document.Components.Schemas != null)
        {
            document.Components.Schemas[name] = schema;
        }
    }

    private static OpenApiOperation? GetOperation(OpenApiPaths paths, string path, HttpMethod method)
    {
        if (!paths.TryGetValue(path, out var pathItem)) return null;
        var operations = pathItem.Operations;
        return operations != null && operations.TryGetValue(method, out var operation) ? operation : null;
    }

    private static void MapGetDailyUsageEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/stats/usage", HttpMethod.Get);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "days",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "model",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/DailyUsageResponse") } }
                    }
                }
            };
        }
    }

    private static void MapGetRecentRequestsEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/stats/requests", HttpMethod.Get);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "limit",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "offset",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "model",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/RecentRequestsResponse") } }
                    }
                }
            };
        }
    }

    private static void MapGetCostComparisonEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/stats/cost-comparison", HttpMethod.Get);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "days",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "model",
                In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/CostComparisonResponse") } }
                    }
                }
            };
        }
    }

    private static void MapGetAllModelsEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models", HttpMethod.Get);
        if (operation != null)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/ModelDashboardItem") } }
                        }
                    }
                }
            };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapGetModelEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models/{name}", HttpMethod.Get);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "name",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/ModelDashboardItem") } }
                    }
                }
            };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapStartModelEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models/{name}/start", HttpMethod.Post);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "name",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/StartModelRequest") } }
                    }
                }
            };
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapStopModelEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models/{name}/stop", HttpMethod.Post);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "name",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapUpdateModelParamsEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models/{name}/params", HttpMethod.Put);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "name",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/UpdateModelParamsRequest") } }
                    }
                }
            };
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/ParameterUpdateResponse") } }
                    }
                }
            };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapGetModelHealthEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/dashboard/models/{name}/health", HttpMethod.Get);
        if (operation != null)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "name",
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "OK",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Extensions = new Dictionary<string, IOpenApiExtension> { ["$ref"] = new SchemaReferenceExtension("#/components/schemas/HealthCheckResponse") } }
                    }
                }
            };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapGetStatusEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/status", HttpMethod.Get);
        if (operation != null)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
        }
    }

    private static void MapPostModelEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/model", HttpMethod.Post);
        if (operation != null)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["model"] = new OpenApiSchema { Type = JsonSchemaType.String }
                            }
                        }
                    }
                }
            };
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void MapPostStopEndpoint(OpenApiDocument document)
    {
        var operation = GetOperation(document.Paths, "/admin/stop", HttpMethod.Post);
        if (operation != null)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
            AddErrorResponses(operation.Responses);
        }
    }

    private static void AddErrorResponses(OpenApiResponses responses)
    {
        responses["400"] = new OpenApiResponse { Description = "Invalid request" };
        responses["404"] = new OpenApiResponse { Description = "Model not found" };
        responses["503"] = new OpenApiResponse { Description = "Failed to start model" };
    }
}
