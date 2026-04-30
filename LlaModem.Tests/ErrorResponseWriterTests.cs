using LlaModem.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlaModem.Tests;

public class ErrorResponseWriterTests
{
    [Fact]
    public async Task WriteAsync_SetsStatusCode()
    {
        var context = new DefaultHttpContext();

        await ErrorResponseWriter.WriteAsync(context, 400, "Bad request", "Invalid input");

        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task WriteAsync_WritesJsonResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.ContentType = "application/json";
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await ErrorResponseWriter.WriteAsync(context, 503, "Service unavailable", "Model not ready");

        responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseBody);
        var body = await reader.ReadToEndAsync();

        var parsed = JsonNode.Parse(body)!;
        Assert.Equal("Service unavailable", parsed["error"]!.GetValue<string>());
        Assert.Equal("Model not ready", parsed["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteAsync_DifferentStatusCodes()
    {
        var context = new DefaultHttpContext();

        await ErrorResponseWriter.WriteAsync(context, 400, "Error A", "Message A");
        Assert.Equal(400, context.Response.StatusCode);

        context = new DefaultHttpContext();
        await ErrorResponseWriter.WriteAsync(context, 401, "Error B", "Message B");
        Assert.Equal(401, context.Response.StatusCode);

        context = new DefaultHttpContext();
        await ErrorResponseWriter.WriteAsync(context, 500, "Error C", "Message C");
        Assert.Equal(500, context.Response.StatusCode);
    }
}
