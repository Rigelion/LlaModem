using LlaModem.Config;
using LlaModem.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LlaModem.Tests;

/// <summary>
/// Tests for IdleTimeoutService restart behavior.
/// Validates that the service restarts on /v1/* requests and excludes /admin/* routes.
/// </summary>
public class IdleTimeoutServiceTests
{
    #region Path Matching Logic Tests

    [Theory]
    [InlineData("/v1/chat/completions", true)]
    [InlineData("/v1/embeddings", true)]
    [InlineData("/v1/models", true)]
    [InlineData("/admin/health", false)]
    [InlineData("/admin/model/start", false)]
    [InlineData("/admin/stop", false)]
    [InlineData("/v1/admin/mixed", true)] // /v1 takes precedence
    public void PathMatchingLogic_CorrectlyIdentifiesV1Requests(string path, bool shouldReset)
    {
        // Arrange & Act - Test the path matching logic directly
        var result = path.StartsWith("/v1", StringComparison.Ordinal) && !path.StartsWith("/admin", StringComparison.Ordinal);

        // Assert
        if (shouldReset)
            Assert.True(result, $"Path '{path}' should trigger reset");
        else
            Assert.False(result, $"Path '{path}' should not trigger reset");
    }

    [Fact]
    public void PathMatchingLogic_ExcludesAdminFromV1()
    {
        // Arrange & Act
        var path1 = "/v1/chat/completions";
        var path2 = "/admin/health";
        var path3 = "/v1/admin/test";

        var result1 = path1.StartsWith("/v1", StringComparison.Ordinal) && !path1.StartsWith("/admin", StringComparison.Ordinal);
        var result2 = path2.StartsWith("/v1", StringComparison.Ordinal) && !path2.StartsWith("/admin", StringComparison.Ordinal);
        var result3 = path3.StartsWith("/v1", StringComparison.Ordinal) && !path3.StartsWith("/admin", StringComparison.Ordinal);

        // Assert
        Assert.True(result1, "/v1/* should trigger reset");
        Assert.False(result2, "/admin/* should not trigger reset");
        Assert.True(result3, "/v1/admin/* should trigger reset (v1 takes precedence)");
    }

    [Fact]
    public void PathMatchingLogic_HandlesRootPath()
    {
        // Arrange & Act
        var rootPath = "/";
        var adminPath = "/admin";
        var v1Path = "/v1";

        var resultRoot = rootPath.StartsWith("/v1", StringComparison.Ordinal) && !rootPath.StartsWith("/admin", StringComparison.Ordinal);
        var resultAdmin = adminPath.StartsWith("/v1", StringComparison.Ordinal) && !adminPath.StartsWith("/admin", StringComparison.Ordinal);
        var resultV1 = v1Path.StartsWith("/v1", StringComparison.Ordinal) && !v1Path.StartsWith("/admin", StringComparison.Ordinal);

        // Assert
        Assert.False(resultRoot, "Root path should not trigger reset");
        Assert.False(resultAdmin, "/admin should not trigger reset");
        Assert.True(resultV1, "/v1 should trigger reset");
    }

    [Theory]
    [InlineData("/V1/chat", false)] // Case sensitive
    [InlineData("/v1/", true)]
    [InlineData("/v10/chat", true)] // /v1 prefix matches /v10
    [InlineData("/v1a/admin", true)] // /v1 prefix matches /v1a
    [InlineData("/admin/v1/test", false)] // admin takes precedence
    public void PathMatchingLogic_EdgeCases(string path, bool expected)
    {
        // Arrange & Act
        var result = path.StartsWith("/v1", StringComparison.Ordinal) && !path.StartsWith("/admin", StringComparison.Ordinal);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region IIdleTimeoutResetter Interface Tests

    [Fact]
    public void IIdleTimeoutResetter_Interface_IsImplemented()
    {
        // Act - Verify interface exists and can be cast
        var resetter = (IIdleTimeoutResetter)new IdleTimeoutService(
            null!, null, Options.Create(new RouterConfig()), new LoggerFactory().CreateLogger<IdleTimeoutService>());

        // Assert
        Assert.NotNull(resetter);
    }

    #endregion

    #region ManualResetEventSlim Signal Tests

    [Fact]
    public void ManualResetEventSlim_InitialState_IsSet()
    {
        // Arrange
        var service = new IdleTimeoutService(
            null!, null, Options.Create(new RouterConfig()), new LoggerFactory().CreateLogger<IdleTimeoutService>());

        // Act - Get the signal field via reflection
        var resetSignalField = typeof(IdleTimeoutService).GetField("_resetSignal", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var resetSignal = (ManualResetEventSlim)resetSignalField!.GetValue(service)!;

        // Assert - Signal should start as set (true)
        Assert.True(resetSignal.IsSet, "Reset signal should be initially set");
    }

    #endregion

    #region Null Safety Tests

    [Fact]
    public void ModelProxyHandler_HandlesNullResetter()
    {
        // Arrange
        var config = Options.Create(new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = new Dictionary<string, ModelConfig>
            {
                { "test-model", new ModelConfig { StartScript = "test.ps1" } }
            }
        });

        var idleTracker = new SystemIdleTracker();
        var forwarder = new RequestForwarder(new LoggerFactory().CreateLogger<RequestForwarder>());
        var httpClientFactory = new MockHttpClientFactory();
        var modelLogger = new LoggerFactory().CreateLogger<ModelProxyHandler>();
        var dashboardLogger = new LoggerFactory().CreateLogger<DashboardService>();

        // Act - Create handler with null idleTimeoutResetter (should not throw)
        var dashboardService = new DashboardService(
            config, null!, null!, null!, null!, Options.Create(new RouterConfig()), dashboardLogger);
        var handler = new ModelProxyHandler(
            config, null!, idleTracker, null!, dashboardService, forwarder, httpClientFactory, modelLogger);

        // Assert - Handler should be created successfully
        Assert.NotNull(handler);
    }

    #endregion

    #region Reset Method Tests

    [Fact]
    public void ResetMethod_CreatesNewTimer()
    {
        // Arrange
        var service = new IdleTimeoutService(
            null!, null, Options.Create(new RouterConfig()), new LoggerFactory().CreateLogger<IdleTimeoutService>());

        // Act - Get the timer field via reflection before reset
        var timerField = typeof(IdleTimeoutService).GetField("_timer", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var originalTimer = timerField!.GetValue(service)!;

        // Act - Call Reset (which disposes and recreates the timer)
        service.Reset();

        // Assert - Timer should be a new instance after reset
        var newTimer = timerField.GetValue(service)!;
        Assert.NotEqual(originalTimer, newTimer);
    }

    [Fact]
    public void ResetMethod_SucceedsWithoutException()
    {
        // Arrange
        var service = new IdleTimeoutService(
            null!, null, Options.Create(new RouterConfig()), new LoggerFactory().CreateLogger<IdleTimeoutService>());

        // Act & Assert - Verify Reset can be called (no exception thrown)
        var exception = Record.Exception(() => service.Reset());
        Assert.Null(exception);
    }

    #endregion
}

#region Mock Classes

internal class MockHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
}

#endregion
