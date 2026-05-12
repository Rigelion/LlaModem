using LlaModem.Config;
using LlaModem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LlaModem.Tests;

/// <summary>
/// Tests for hybrid model backend routing (Option C).
/// Validates that models can use dedicated backend URLs while maintaining backward compatibility.
/// </summary>
public class HybridRoutingTests
{
    #region ModelConfig Tests

    [Fact]
    public void ModelConfig_WithoutBackendUrl_UsesDefaultValue()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1"
        };

        // Act
        var backendUrl = config.BackendUrl ?? "http://localhost:8001";

        // Assert
        Assert.Equal("http://localhost:8001", backendUrl);
    }

    [Fact]
    public void ModelConfig_WithBackendUrl_UsesCustomUrl()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1",
            BackendUrl = "http://localhost:8003"
        };

        // Act
        var backendUrl = config.BackendUrl ?? "http://localhost:8001";

        // Assert
        Assert.Equal("http://localhost:8003", backendUrl);
    }

    [Fact]
    public void ModelConfig_WithTrailingSlash_BackendUrl_PreservesIt()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1",
            BackendUrl = "http://localhost:8003/"
        };

        // Act
        var backendUrl = config.BackendUrl ?? "http://localhost:8001";

        // Assert
        Assert.Equal("http://localhost:8003/", backendUrl);
    }

    [Fact]
    public void ModelConfig_WithCustomPort_HostIsCorrect()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1",
            BackendUrl = "http://192.168.1.100:9999"
        };

        // Act
        var backendUrl = config.BackendUrl ?? "http://localhost:8001";

        // Assert
        Assert.Equal("http://192.168.1.100:9999", backendUrl);
    }

    #endregion

    #region AppConfig Integration Tests

    [Fact]
    public void AppConfig_MultipleModels_WithMixedBackendUrls()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "qwen36-smart", new ModelConfig { StartScript = "F:/llama/llama-qwen36-SMART.ps1" } },
            { "qwen36-optimized", new ModelConfig { StartScript = "F:/llama/llama-qwen36-OPTIMIZED.ps1" } },
            { "squeez-2b", new ModelConfig { StartScript = "F:/llama/squeez-2b.ps1", BackendUrl = "http://localhost:8003" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        // Act
        var qwenUrl = appConfig.Models["qwen36-smart"].BackendUrl ?? appConfig.BackendUrl;
        var squeezUrl = appConfig.Models["squeez-2b"].BackendUrl ?? appConfig.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", qwenUrl);
        Assert.Equal("http://localhost:8003", squeezUrl);
    }

    [Fact]
    public void AppConfig_OnlyModelsWithBackendUrl_AllUseDedicatedPorts()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "model1", new ModelConfig { StartScript = "script1.ps1", BackendUrl = "http://localhost:8001" } },
            { "model2", new ModelConfig { StartScript = "script2.ps1", BackendUrl = "http://localhost:8002" } },
            { "model3", new ModelConfig { StartScript = "script3.ps1", BackendUrl = "http://localhost:8003" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        // Act
        var url1 = appConfig.Models["model1"].BackendUrl ?? appConfig.BackendUrl;
        var url2 = appConfig.Models["model2"].BackendUrl ?? appConfig.BackendUrl;
        var url3 = appConfig.Models["model3"].BackendUrl ?? appConfig.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", url1);
        Assert.Equal("http://localhost:8002", url2);
        Assert.Equal("http://localhost:8003", url3);
    }

    [Fact]
    public void AppConfig_OnlyModelsWithoutBackendUrl_AllUseGlobal()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "model1", new ModelConfig { StartScript = "script1.ps1" } },
            { "model2", new ModelConfig { StartScript = "script2.ps1" } },
            { "model3", new ModelConfig { StartScript = "script3.ps1" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        // Act
        var url1 = appConfig.Models["model1"].BackendUrl ?? appConfig.BackendUrl;
        var url2 = appConfig.Models["model2"].BackendUrl ?? appConfig.BackendUrl;
        var url3 = appConfig.Models["model3"].BackendUrl ?? appConfig.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", url1);
        Assert.Equal("http://localhost:8001", url2);
        Assert.Equal("http://localhost:8001", url3);
    }

    #endregion

    #region ModelProxyHandler Integration Tests

    [Fact]
    public async Task ModelProxyHandler_ResolveBackendUrl_UsesModelSpecificUrl()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "qwen36-smart", new ModelConfig { StartScript = "F:/llama/llama-qwen36-SMART.ps1" } },
            { "squeez-2b", new ModelConfig { StartScript = "F:/llama/squeez-2b.ps1", BackendUrl = "http://localhost:8003" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Llama-Model"] = "squeez-2b";
        httpContext.Request.Path = "/v1/chat/completions";

        // Act
        var modelConfig = appConfig.Models["squeez-2b"];
        var backendUrl = modelConfig.BackendUrl ?? appConfig.BackendUrl;
        var targetUrl = RequestForwarder.BuildTargetUrl(backendUrl, httpContext.Request);

        // Assert
        Assert.Equal("http://localhost:8003", backendUrl);
        Assert.Equal("http://localhost:8003/chat/completions", targetUrl);
    }

    [Fact]
    public async Task ModelProxyHandler_ResolveBackendUrl_UsesGlobalUrlForModelsWithoutCustom()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "qwen36-smart", new ModelConfig { StartScript = "F:/llama/llama-qwen36-SMART.ps1" } },
            { "squeez-2b", new ModelConfig { StartScript = "F:/llama/squeez-2b.ps1", BackendUrl = "http://localhost:8003" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Llama-Model"] = "qwen36-smart";
        httpContext.Request.Path = "/v1/chat/completions";

        // Act
        var modelConfig = appConfig.Models["qwen36-smart"];
        var backendUrl = modelConfig.BackendUrl ?? appConfig.BackendUrl;
        var targetUrl = RequestForwarder.BuildTargetUrl(backendUrl, httpContext.Request);

        // Assert
        Assert.Equal("http://localhost:8001", backendUrl);
        Assert.Equal("http://localhost:8001/chat/completions", targetUrl);
    }

    [Fact]
    public async Task ModelProxyHandler_ResolveBackendUrl_HandlesTrailingSlash()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "squeez-2b", new ModelConfig { StartScript = "F:/llama/squeez-2b.ps1", BackendUrl = "http://localhost:8003/" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Llama-Model"] = "squeez-2b";
        httpContext.Request.Path = "/v1/chat/completions";

        // Act
        var modelConfig = appConfig.Models["squeez-2b"];
        var backendUrl = modelConfig.BackendUrl ?? appConfig.BackendUrl;
        var targetUrl = RequestForwarder.BuildTargetUrl(backendUrl, httpContext.Request);

        // Assert
        Assert.Equal("http://localhost:8003/", backendUrl);
        Assert.Equal("http://localhost:8003/chat/completions", targetUrl);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void ModelConfig_WithEmptyStringBackendUrl_UsesFallback()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1",
            BackendUrl = ""
        };

        // Act
        var backendUrl = string.IsNullOrWhiteSpace(config.BackendUrl) 
            ? "http://localhost:8001" 
            : config.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", backendUrl);
    }

    [Fact]
    public void ModelConfig_WithWhitespaceBackendUrl_UsesFallback()
    {
        // Arrange
        var config = new ModelConfig
        {
            StartScript = "F:/llama/test.ps1",
            BackendUrl = "   "
        };

        // Act
        var backendUrl = string.IsNullOrWhiteSpace(config.BackendUrl)
            ? "http://localhost:8001"
            : config.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", backendUrl);
    }

    [Fact]
    public void AppConfig_WithHttpAndHttpsUrls_MixedProtocolSupported()
    {
        // Arrange
        var models = new Dictionary<string, ModelConfig>
        {
            { "local-model", new ModelConfig { StartScript = "script.ps1", BackendUrl = "http://localhost:8001" } },
            { "remote-model", new ModelConfig { StartScript = "script.ps1", BackendUrl = "https://remote.example.com:443" } }
        };

        var appConfig = new AppConfig
        {
            BackendUrl = "http://localhost:8001",
            Models = models
        };

        // Act
        var localUrl = appConfig.Models["local-model"].BackendUrl ?? appConfig.BackendUrl;
        var remoteUrl = appConfig.Models["remote-model"].BackendUrl ?? appConfig.BackendUrl;

        // Assert
        Assert.Equal("http://localhost:8001", localUrl);
        Assert.Equal("https://remote.example.com:443", remoteUrl);
    }

    #endregion
}
