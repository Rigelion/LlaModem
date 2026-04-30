using LlaModem.Services;
using Microsoft.AspNetCore.Http;

namespace LlaModem.Tests;

public class LaunchParamParserTests
{
    [Fact]
    public async Task ParseAsync_NoHeaders_ReturnsNull()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(headers: new Dictionary<string, string>());

        var result = await parser.ParseAsync(context, context.Request);

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_TemperatureOnly_ReturnsParamsWithTemperature()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string> { { "X-Llama-Temperature", "0.7" } });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.NotNull(result);
        Assert.Equal(0.7, result.Temperature);
        Assert.Null(result.TopP);
        Assert.Null(result.PresencePenalty);
    }

    [Fact]
    public async Task ParseAsync_AllHeaders_ReturnsAllParams()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-Temperature", "0.5" },
            { "X-Llama-TopP", "0.9" },
            { "X-Llama-PresencePenalty", "-0.5" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.NotNull(result);
        Assert.Equal(0.5, result.Temperature);
        Assert.Equal(0.9, result.TopP);
        Assert.Equal(-0.5, result.PresencePenalty);
    }

    [Fact]
    public async Task ParseAsync_PartialHeaders_ReturnsPartialParams()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-Temperature", "0.8" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.NotNull(result);
        Assert.Equal(0.8, result.Temperature);
        Assert.Null(result.TopP);
        Assert.Null(result.PresencePenalty);
    }

    [Fact]
    public async Task ParseAsync_InvalidTemperature_ReturnsError()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-Temperature", "not-a-number" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.Null(result);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task ParseAsync_InvalidTopP_ReturnsError()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-TopP", "abc" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.Null(result);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task ParseAsync_NegativePresencePenalty_ParsesCorrectly()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-PresencePenalty", "-1.0" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.NotNull(result);
        Assert.Equal(-1.0, result.PresencePenalty);
    }

    [Fact]
    public async Task ParseAsync_ZeroValues_ParsesCorrectly()
    {
        var parser = new LaunchParamParser();
        var context = CreateHttpContext(new Dictionary<string, string>
        {
            { "X-Llama-Temperature", "0" },
            { "X-Llama-TopP", "0" },
            { "X-Llama-PresencePenalty", "0" }
        });

        var result = await parser.ParseAsync(context, context.Request);

        Assert.NotNull(result);
        Assert.Equal(0.0, result.Temperature);
        Assert.Equal(0.0, result.TopP);
        Assert.Equal(0.0, result.PresencePenalty);
    }

    private static HttpContext CreateHttpContext(Dictionary<string, string> headers)
    {
        var httpContext = new DefaultHttpContext();
        foreach (var (key, value) in headers)
        {
            httpContext.Request.Headers[key] = value;
        }
        return httpContext;
    }
}
