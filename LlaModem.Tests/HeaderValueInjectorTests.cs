using LlaModem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlaModem.Tests;

public class HeaderValueInjectorTests
{
    [Fact]
    public void Constructor_WithDisabledFeature_SkipsInjection()
    {
        var injector = new HeaderValueInjector(enabled: false, new Dictionary<string, string> { { "x-test", "test" } });
        Assert.NotNull(injector);
    }

    [Fact]
    public void Constructor_WithEmptyMappings_SkipsInjection()
    {
        var injector = new HeaderValueInjector(enabled: true, new Dictionary<string, string>());
        Assert.NotNull(injector);
    }

    [Fact]
    public async Task InjectAsync_NoContentType_ReturnsEarly()
    {
        var injector = new HeaderValueInjector(enabled: true, new Dictionary<string, string> { { "x-test", "test" } });
        var context = CreateHttpContext(contentType: null, body: "{\"key\":\"value\"}");

        await injector.InjectAsync(context, NullLogger.Instance);

        // Body should be unchanged since content type check fails
        var bodyText = await ReadBodyAsync(context);
        Assert.Equal("{\"key\":\"value\"}", bodyText);
    }

    [Fact]
    public async Task InjectAsync_NotJson_ReturnsEarly()
    {
        var injector = new HeaderValueInjector(enabled: true, new Dictionary<string, string> { { "x-test", "test" } });
        var context = CreateHttpContext(contentType: "text/plain", body: "not json");

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        Assert.Equal("not json", bodyText);
    }

    [Fact]
    public async Task InjectAsync_JsonArray_ReturnsEarly()
    {
        var injector = new HeaderValueInjector(enabled: true, new Dictionary<string, string> { { "x-test", "test" } });
        var context = CreateHttpContext(contentType: "application/json", body: "[1,2,3]");

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        Assert.Equal("[1,2,3]", bodyText);
    }

    [Fact]
    public async Task InjectAsync_IntHeader_InjectsAsNumber()
    {
        var mappings = new Dictionary<string, string> { { "x-llama-temperature", "temperature" } };
        var injector = new HeaderValueInjector(enabled: true, mappings);
        var context = CreateHttpContext(
            contentType: "application/json",
            body: "{\"messages\":[]}",
            headers: new Dictionary<string, string> { { "x-llama-temperature", "0.7" } });

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        var parsed = JsonNode.Parse(bodyText)!;
        Assert.NotNull(parsed["temperature"]);
        Assert.Equal(JsonValueKind.Number, parsed["temperature"]!.GetValueKind());
    }

    [Fact]
    public async Task InjectAsync_BoolHeader_InjectsAsBoolean()
    {
        var mappings = new Dictionary<string, string> { { "x-debug", "debug" } };
        var injector = new HeaderValueInjector(enabled: true, mappings);
        var context = CreateHttpContext(
            contentType: "application/json",
            body: "{\"messages\":[]}",
            headers: new Dictionary<string, string> { { "x-debug", "true" } });

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        var parsed = JsonNode.Parse(bodyText)!;
        Assert.NotNull(parsed["debug"]);
        Assert.Equal(JsonValueKind.True, parsed["debug"]!.GetValueKind());
    }

    [Fact]
    public async Task InjectAsync_StringHeader_InjectsAsString()
    {
        var mappings = new Dictionary<string, string> { { "x-client-id", "clientId" } };
        var injector = new HeaderValueInjector(enabled: true, mappings);
        var context = CreateHttpContext(
            contentType: "application/json",
            body: "{\"messages\":[]}",
            headers: new Dictionary<string, string> { { "x-client-id", "my-client-123" } });

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        var parsed = JsonNode.Parse(bodyText)!;
        Assert.NotNull(parsed["clientId"]);
        Assert.Equal(JsonValueKind.String, parsed["clientId"]!.GetValueKind());
        Assert.Equal("my-client-123", parsed["clientId"]!.GetValue<string>());
    }

    [Fact]
    public async Task InjectAsync_MissingHeader_SkipsInjection()
    {
        var mappings = new Dictionary<string, string> { { "x-missing", "missingKey" } };
        var injector = new HeaderValueInjector(enabled: true, mappings);
        var context = CreateHttpContext(
            contentType: "application/json",
            body: "{\"messages\":[]}",
            headers: new Dictionary<string, string>());

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        Assert.Equal("{\"messages\":[]}", bodyText);
    }

    [Fact]
    public async Task InjectAsync_MultipleHeaders_InjectsAll()
    {
        var mappings = new Dictionary<string, string>
        {
            { "x-llama-temperature", "temperature" },
            { "x-client-id", "clientId" }
        };
        var injector = new HeaderValueInjector(enabled: true, mappings);
        var context = CreateHttpContext(
            contentType: "application/json",
            body: "{\"messages\":[]}",
            headers: new Dictionary<string, string>
            {
                { "x-llama-temperature", "0.5" },
                { "x-client-id", "test-client" }
            });

        await injector.InjectAsync(context, NullLogger.Instance);

        var bodyText = await ReadBodyAsync(context);
        var parsed = JsonNode.Parse(bodyText)!;
        Assert.NotNull(parsed["temperature"]);
        Assert.NotNull(parsed["clientId"]);
    }

    private static HttpContext CreateHttpContext(
        string? contentType,
        string body,
        Dictionary<string, string>? headers = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Clear();

        if (contentType != null)
            httpContext.Request.ContentType = contentType;

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                httpContext.Request.Headers[key] = value;
            }
        }

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;

        return httpContext;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
