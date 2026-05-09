using LlaModem.Models;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace LlaModem.Tests;

public class ResponseUsageMiddlewareTests
{
    [Fact]
    public async Task Invoke_CapturesUsageFromChatCompletions()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"llama3.2\",\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":50,\"total_tokens\":60}}"),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Model == "llama3.2" &&
            e.Route == "/v1/chat/completions" &&
            e.Usage.PromptTokens == 10 &&
            e.Usage.CompletionTokens == 50 &&
            e.Usage.TotalTokens == 60)), Times.Once);
    }

    [Fact]
    public async Task Invoke_CapturesUsageFromCompletions()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"mistral\",\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":20,\"total_tokens\":25}}"),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Model == "mistral" &&
            e.Usage.TotalTokens == 25)), Times.Once);
    }

    [Fact]
    public async Task Invoke_SkipsNonProxyRoutes()
    {
        // Arrange — next handler just passes through without touching usage
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            next: _ => Task.CompletedTask,
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — usage service never called for non-proxy routes
        mockService.Verify(s => s.Record(It.IsAny<SessionEntry>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_SkipsAdminRoutes()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            next: _ => Task.CompletedTask,
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/admin/status";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockService.Verify(s => s.Record(It.IsAny<SessionEntry>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_HandlesResponseWithoutUsage()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"choices\":[{\"text\":\"hello\"}]}"),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — no record called because there's no usage field
        mockService.Verify(s => s.Record(It.IsAny<SessionEntry>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_PassesOriginalResponseBodyThrough()
    {
        // Arrange — use a non-disposing wrapper so ASP.NET Core doesn't kill the stream
        var expectedBody = "{\"model\":\"llama3.2\",\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":50,\"total_tokens\":60}}";
        var originalStream = new NonDisposingMemoryStream();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler(expectedBody),
            Mock.Of<IUsageService>(),
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = originalStream;

        // Act
        await middleware.InvokeAsync(context);

        // Assert — original body is passed through unchanged
        originalStream.Position = 0;
        var actualBody = new StreamReader(originalStream).ReadToEnd();
        Assert.Equal(expectedBody, actualBody);
    }

    [Fact]
    public async Task Invoke_HandlesMissingUsageFieldsGracefully()
    {
        // Arrange — response has usage object but with zero/missing fields
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"usage\":{}}"),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — should still record with zero values
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Usage.PromptTokens == 0 &&
            e.Usage.CompletionTokens == 0 &&
            e.Usage.TotalTokens == 0)), Times.Once);
    }

    [Fact]
    public async Task Invoke_HandlesEmptyResponseBody()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler(""),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act — should not throw
        await middleware.InvokeAsync(context);

        // Assert — no record called for empty body
        mockService.Verify(s => s.Record(It.IsAny<SessionEntry>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_CapturesTimingsFromLlamaServerResponse()
    {
        // Arrange — llama-server response with timings
        var mockService = new Mock<IUsageService>();
        var response = "{\"model\":\"llama3.2\",\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":50,\"total_tokens\":60},\"timings\":{\"prompt_ms\":120.5,\"predicted_ms\":450.3,\"prompt_per_token_ms\":12.05,\"predicted_per_token_ms\":9.006,\"cache_n\":15}}";
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler(response),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Usage.PromptTokens == 10 &&
            e.Usage.CompletionTokens == 50 &&
            e.Usage.TotalTokens == 60 &&
            e.Usage.Timings.HasValue &&
            e.Usage.Timings.Value.PromptMs == 120.5 &&
            e.Usage.Timings.Value.CompletionMs == 450.3 &&
            e.Usage.Timings.Value.CacheHits == 15)), Times.Once);
    }

    [Fact]
    public async Task Invoke_HandlesResponseWithoutTimingsGracefully()
    {
        // Arrange — response without timings field
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":50,\"total_tokens\":60}}"),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — timings should be null
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Usage.PromptTokens == 10 &&
            !e.Usage.Timings.HasValue)), Times.Once);
    }

    [Fact]
    public async Task Invoke_HandlesPartialTimings()
    {
        // Arrange — timings with only some fields present
        var mockService = new Mock<IUsageService>();
        var response = "{\"model\":\"test\",\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":20,\"total_tokens\":25},\"timings\":{\"prompt_ms\":100}}";
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler(response),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert — only prompt_ms present, others null
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Usage.Timings.HasValue &&
            e.Usage.Timings.Value.PromptMs == 100 &&
            !e.Usage.Timings.Value.CompletionMs.HasValue)), Times.Once);
    }

    [Fact]
    public async Task Invoke_CapturesTimingsFromNDJSON()
    {
        // Arrange — NDJSON with timings in last line
        var ndjson = "{\"model\":\"test\",\"choices\":[{\"delta\":{\"content\":\"h\"}}]}\n{\"model\":\"test\",\"choices\":[{\"delta\":{\"content\":\"i\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":10,\"total_tokens\":13},\"timings\":{\"prompt_ms\":50.0,\"predicted_ms\":200.0,\"cache_n\":5}}";
        var mockService = new Mock<IUsageService>();
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler(ndjson),
            mockService.Object,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockService.Verify(s => s.Record(It.Is<SessionEntry>(e =>
            e.Usage.PromptTokens == 3 &&
            e.Usage.CompletionTokens == 10 &&
            e.Usage.Timings.HasValue &&
            e.Usage.Timings.Value.CacheHits == 5)), Times.Once);
    }

    [Fact]
    public async Task Invoke_WhenNoUsageService_DoesNotThrow()
    {
        // Arrange — usage service is null
        var middleware = new ResponseUsageMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2,\"total_tokens\":3}}"),
            null,
            Mock.Of<ILogger<ResponseUsageMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act — should not throw when usage service is not configured
        // If it throws, the test fails
        await middleware.InvokeAsync(context);

        // Assert — if we get here, no exception was thrown
        Assert.True(true);
    }

    /// <summary>
    /// MemoryStream that doesn't dispose when disposed — survives ASP.NET Core's response cleanup.
    /// </summary>
    private sealed class NonDisposingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            // Suppress disposal so the stream remains usable after the response completes
        }
    }

    private static RequestDelegate CreateNextHandler(string responseBody)
    {
        return async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(responseBody);
        };
    }
}
