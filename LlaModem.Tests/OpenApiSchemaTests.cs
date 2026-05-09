using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LlaModem.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LlaModem.Tests;

public class OpenApiSchemaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OpenApiSchemaTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_ReturnsValidJson()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(json);
    }

    [Fact]
    public async Task OpenApiDocument_HasAllRequiredSchemas()
    {
        // Arrange
        var expectedSchemas = new[]
        {
            "DateRange",
            "ModelDashboardItem",
            "StartModelRequest",
            "UpdateModelParamsRequest",
            "HealthCheckResponse",
            "ParameterUpdateResponse",
            "DailyUsageResponse",
            "RecentRequestsResponse",
            "CostComparisonResponse"
        };

        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var schemaNames = new HashSet<string>();
        foreach (var prop in schemas.EnumerateObject())
        {
            schemaNames.Add(prop.Name);
        }

        // Assert
        foreach (var expected in expectedSchemas)
        {
            Assert.True(schemaNames.Contains(expected), $"Schema '{expected}' not found in OpenAPI document");
        }
    }

    [Fact]
    public async Task StatsUsageEndpoint_HasCorrectSchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var usagePath = document.RootElement.GetProperty("paths").GetProperty("/admin/stats/usage");
        var getOperation = usagePath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert - should have $ref, not unknown
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Response schema should have $ref property");
        Assert.Equal("#/components/schemas/DailyUsageResponse", refProp.GetString());
    }

    [Fact]
    public async Task StatsRequestsEndpoint_HasCorrectSchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var requestsPath = document.RootElement.GetProperty("paths").GetProperty("/admin/stats/requests");
        var getOperation = requestsPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Response schema should have $ref property");
        Assert.Equal("#/components/schemas/RecentRequestsResponse", refProp.GetString());
    }

    [Fact]
    public async Task StatsCostComparisonEndpoint_HasCorrectSchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var costPath = document.RootElement.GetProperty("paths").GetProperty("/admin/stats/cost-comparison");
        var getOperation = costPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Response schema should have $ref property");
        Assert.Equal("#/components/schemas/CostComparisonResponse", refProp.GetString());
    }

    [Fact]
    public async Task DashboardModelsEndpoint_HasArraySchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var modelsPath = document.RootElement.GetProperty("paths").GetProperty("/admin/dashboard/models");
        var getOperation = modelsPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert - should be array with $ref in items
        Assert.True(schema.TryGetProperty("type", out var typeProp), "Schema should have type property");
        Assert.Equal("array", typeProp.GetString());
        Assert.True(schema.TryGetProperty("items", out var itemsProp), "Array schema should have items property");
        Assert.True(itemsProp.TryGetProperty("$ref", out var refProp), "Items should have $ref property");
        Assert.Equal("#/components/schemas/ModelDashboardItem", refProp.GetString());
    }

    [Fact]
    public async Task DashboardModelByIdEndpoint_HasCorrectSchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var modelPath = document.RootElement.GetProperty("paths").GetProperty("/admin/dashboard/models/{name}");
        var getOperation = modelPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Response schema should have $ref property");
        Assert.Equal("#/components/schemas/ModelDashboardItem", refProp.GetString());
    }

    [Fact]
    public async Task DashboardStartModelEndpoint_HasRequestBodySchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var startPath = document.RootElement.GetProperty("paths").GetProperty("/admin/dashboard/models/{name}/start");
        var postOperation = startPath.GetProperty("post");
        var requestBody = postOperation.GetProperty("requestBody");
        var content = requestBody.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Request body schema should have $ref property");
        Assert.Equal("#/components/schemas/StartModelRequest", refProp.GetString());
    }

    [Fact]
    public async Task DashboardUpdateParamsEndpoint_HasRequestBodyAndResponseSchemas()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var paramsPath = document.RootElement.GetProperty("paths").GetProperty("/admin/dashboard/models/{name}/params");
        var putOperation = paramsPath.GetProperty("put");

        // Request body
        var requestBody = putOperation.GetProperty("requestBody");
        var requestContent = requestBody.GetProperty("content").GetProperty("application/json");
        var requestSchema = requestContent.GetProperty("schema");
        Assert.True(requestSchema.TryGetProperty("$ref", out var requestRef), "Request body should have $ref");
        Assert.Equal("#/components/schemas/UpdateModelParamsRequest", requestRef.GetString());

        // Response
        var responses = putOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var responseContent = response200.GetProperty("content").GetProperty("application/json");
        var responseSchema = responseContent.GetProperty("schema");
        Assert.True(responseSchema.TryGetProperty("$ref", out var responseRef), "Response should have $ref");
        Assert.Equal("#/components/schemas/ParameterUpdateResponse", responseRef.GetString());
    }

    [Fact]
    public async Task DashboardModelHealthEndpoint_HasCorrectSchema()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var healthPath = document.RootElement.GetProperty("paths").GetProperty("/admin/dashboard/models/{name}/health");
        var getOperation = healthPath.GetProperty("get");
        var responses = getOperation.GetProperty("responses");
        var response200 = responses.GetProperty("200");
        var content = response200.GetProperty("content").GetProperty("application/json");
        var schema = content.GetProperty("schema");

        // Assert
        Assert.True(schema.TryGetProperty("$ref", out var refProp), "Response schema should have $ref property");
        Assert.Equal("#/components/schemas/HealthCheckResponse", refProp.GetString());
    }

    [Fact]
    public async Task AdminEndpoints_HaveErrorResponses()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        // Check dashboard endpoints have 400/404/503
        var endpoints = new[]
        {
            new { Path = "/admin/dashboard/models/{name}", Method = "get" },
            new { Path = "/admin/dashboard/models/{name}/start", Method = "post" },
            new { Path = "/admin/dashboard/models/{name}/stop", Method = "post" },
            new { Path = "/admin/dashboard/models/{name}/params", Method = "put" },
            new { Path = "/admin/dashboard/models/{name}/health", Method = "get" }
        };

        foreach (var endpoint in endpoints)
        {
            var path = document.RootElement.GetProperty("paths").GetProperty(endpoint.Path);
            var operation = path.GetProperty(endpoint.Method);
            var responses = operation.GetProperty("responses");

            Assert.True(responses.TryGetProperty("400", out var _), $"Endpoint {endpoint.Path} should have 400 response");
            Assert.True(responses.TryGetProperty("404", out var _), $"Endpoint {endpoint.Path} should have 404 response");
            Assert.True(responses.TryGetProperty("503", out var _), $"Endpoint {endpoint.Path} should have 503 response");
        }
    }

    [Fact]
    public async Task StatsEndpoints_HaveQueryParameters()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        // Check /admin/stats/usage has days and model query params
        var usagePath = document.RootElement.GetProperty("paths").GetProperty("/admin/stats/usage");
        var getOperation = usagePath.GetProperty("get");
        var parameters = getOperation.GetProperty("parameters");

        var paramNames = new HashSet<string>();
        foreach (var param in parameters.EnumerateArray())
        {
            var name = param.GetProperty("name").GetString();
            paramNames.Add(name);
        }

        Assert.True(paramNames.Contains("days"), "Usage endpoint should have 'days' parameter");
        Assert.True(paramNames.Contains("model"), "Usage endpoint should have 'model' parameter");

        // Check /admin/stats/requests has limit, offset, model params
        var requestsPath = document.RootElement.GetProperty("paths").GetProperty("/admin/stats/requests");
        var requestsOp = requestsPath.GetProperty("get");
        var requestParams = requestsOp.GetProperty("parameters");

        var requestParamNames = new HashSet<string>();
        foreach (var param in requestParams.EnumerateArray())
        {
            var name = param.GetProperty("name").GetString();
            requestParamNames.Add(name);
        }

        Assert.True(requestParamNames.Contains("limit"), "Requests endpoint should have 'limit' parameter");
        Assert.True(requestParamNames.Contains("offset"), "Requests endpoint should have 'offset' parameter");
        Assert.True(requestParamNames.Contains("model"), "Requests endpoint should have 'model' parameter");
    }

    [Fact]
    public async Task OpenApiDocument_HasCorrectMetadata()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        // Assert
        Assert.True(document.RootElement.TryGetProperty("openapi", out var openApiProp), "Document should have 'openapi' property");
        Assert.True(document.RootElement.TryGetProperty("info", out var infoProp), "Document should have 'info' property");
        Assert.True(document.RootElement.TryGetProperty("paths", out var pathsProp), "Document should have 'paths' property");
        Assert.True(document.RootElement.TryGetProperty("components", out var componentsProp), "Document should have 'components' property");
    }

    [Fact]
    public async Task DateRangeSchema_HasCorrectStructure()
    {
        // Act
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var dateRange = schemas.GetProperty("DateRange");

        // Assert - should be object type
        Assert.True(dateRange.TryGetProperty("type", out var typeProp), "DateRange should have type property");
        Assert.Equal("object", typeProp.GetString());

        // Assert - should have properties
        Assert.True(dateRange.TryGetProperty("properties", out var propsProp), "DateRange should have properties");
        var props = propsProp.GetProperty("from");
        Assert.True(props.TryGetProperty("type", out var fromType), "from property should have type");
        Assert.Equal("string", fromType.GetString());
    }
}
