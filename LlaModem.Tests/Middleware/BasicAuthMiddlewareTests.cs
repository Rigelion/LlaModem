using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using LlaModem.Middleware;
using LlaModem.Config;

namespace llamodem.Tests.Middleware;

public class BasicAuthMiddlewareTests
{
    private readonly RequestDelegate _next = context => Task.CompletedTask;
    
    [Fact]
    public async Task ValidCredentials_PassesAuthentication()
    {
        // Arrange
        var config = new RouterConfig
        {
            AuthUsername = "admin",
            AuthPassword = "secret123"
        };
        
        var mockOptions = Mock.Of<IOptions<RouterConfig>>(m => m.Value == config);
        var mockLogger = new Mock<ILogger<BasicAuthMiddleware>>();
        var middleware = new BasicAuthMiddleware(_next, mockOptions, mockLogger.Object);
        
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret123"));
        
        // Act
        await middleware.InvokeAsync(context);
        
        // Assert
        Assert.Equal(200, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var config = new RouterConfig
        {
            AuthUsername = "admin",
            AuthPassword = "secret123"
        };
        
        var mockOptions = Mock.Of<IOptions<RouterConfig>>(m => m.Value == config);
        var middleware = new BasicAuthMiddleware(_next, mockOptions, Mock.Of<ILogger<BasicAuthMiddleware>>());
        
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrongpass"));
        
        // Act
        await middleware.InvokeAsync(context);
        
        // Assert
        Assert.Equal(401, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task MissingAuthHeader_ReturnsUnauthorized()
    {
        // Arrange
        var config = new RouterConfig
        {
            AuthUsername = "admin",
            AuthPassword = "secret123"
        };
        
        var mockOptions = Mock.Of<IOptions<RouterConfig>>(m => m.Value == config);
        var middleware = new BasicAuthMiddleware(_next, mockOptions, Mock.Of<ILogger<BasicAuthMiddleware>>());
        
        var context = new DefaultHttpContext();
        
        // Act
        await middleware.InvokeAsync(context);
        
        // Assert
        Assert.Equal(401, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvalidBase64Encoding_ReturnsUnauthorized()
    {
        // Arrange
        var config = new RouterConfig
        {
            AuthUsername = "admin",
            AuthPassword = "secret123"
        };
        
        var mockOptions = Mock.Of<IOptions<RouterConfig>>(m => m.Value == config);
        var middleware = new BasicAuthMiddleware(_next, mockOptions, Mock.Of<ILogger<BasicAuthMiddleware>>());
        
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Basic invalid!!!";
        
        // Act
        await middleware.InvokeAsync(context);
        
        // Assert
        Assert.Equal(401, context.Response.StatusCode);
    }
    
    [Fact]
    public void ConstantTimeComparison_LengthMismatch_ReturnsFalse()
    {
        // Arrange
        var config = Mock.Of<IOptions<RouterConfig>>(m => m.Value == new RouterConfig { AuthUsername = "a", AuthPassword = "b" });
        var middleware = new BasicAuthMiddleware(_next, config, null!);
        
        // Act - use reflection to call private method
        var method = typeof(BasicAuthMiddleware).GetMethod("CompareConstantTime", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (bool)method.Invoke(null, new object?[] { "short", "muchlongerstring" })!;
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void ConstantTimeComparison_EqualStrings_ReturnsTrue()
    {
        // Arrange
        var config = Mock.Of<IOptions<RouterConfig>>(m => m.Value == new RouterConfig { AuthUsername = "a", AuthPassword = "b" });
        var middleware = new BasicAuthMiddleware(_next, config, null!);
        
        // Act
        var method = typeof(BasicAuthMiddleware).GetMethod("CompareConstantTime", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (bool)method.Invoke(null, new object?[] { "same", "same" })!;
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void Constructor_NullNext_ThrowsArgumentNullException()
    {
        // Arrange
        var config = Mock.Of<IOptions<RouterConfig>>();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BasicAuthMiddleware(null!, config, null!));
    }
    
    [Fact]
    public void Constructor_NullConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var nullOptions = Mock.Of<IOptions<RouterConfig>>(m => m.Value == null);
        
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => new BasicAuthMiddleware(_next, nullOptions, null!));
        Assert.Contains("RouterConfig not configured", ex.Message);
    }
}
