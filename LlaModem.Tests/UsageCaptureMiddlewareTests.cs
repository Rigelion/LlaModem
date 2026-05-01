using LlaModem.Models;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace LlaModem.Tests;

public class UsageCaptureMiddlewareTests
{
    [Fact]
    public async Task Invoke_CapturesUsageFromChatCompletions()
    {
        // Arrange
        var mockService = new Mock<IUsageService>();
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler("{\"model\":\"llama3.2\",\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":50,\"total_tokens\":60}}"),
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler("{\"model\":\"mistral\",\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":20,\"total_tokens\":25}}"),
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            next: _ => Task.CompletedTask,
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            next: _ => Task.CompletedTask,
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"choices\":[{\"text\":\"hello\"}]}"),
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler(expectedBody),
            Mock.Of<IUsageService>(),
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler("{\"model\":\"test\",\"usage\":{}}"),
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

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
        var middleware = new UsageCaptureMiddleware(
            CreateNextHandler(""),
            mockService.Object,
            Mock.Of<ILogger<UsageCaptureMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/chat/completions";
        context.Response.Body = new MemoryStream();

        // Act — should not throw
        await middleware.InvokeAsync(context);

        // Assert — no record called for empty body
        mockService.Verify(s => s.Record(It.IsAny<SessionEntry>()), Times.Never);
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
