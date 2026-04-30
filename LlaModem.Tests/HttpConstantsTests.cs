using LlaModem.Services;

namespace LlaModem.Tests;

public class HttpConstantsTests
{
    [Fact]
    public void ExcludedHeaders_ContainsExpectedHopByHopHeaders()
    {
        Assert.Contains("Host", HttpConstants.ExcludedHeaders);
        Assert.Contains("Connection", HttpConstants.ExcludedHeaders);
        Assert.Contains("Keep-Alive", HttpConstants.ExcludedHeaders);
        Assert.Contains("Transfer-Encoding", HttpConstants.ExcludedHeaders);
        Assert.Contains("Upgrade", HttpConstants.ExcludedHeaders);
    }

    [Fact]
    public void ExcludedHeaders_IsCaseInsensitive()
    {
        Assert.Contains("host", HttpConstants.ExcludedHeaders);
        Assert.Contains("HOST", HttpConstants.ExcludedHeaders);
        Assert.Contains("Connection", HttpConstants.ExcludedHeaders);
        Assert.Contains("CONNECTION", HttpConstants.ExcludedHeaders);
    }

    [Fact]
    public void ExcludedHeaders_DoesNotContainForwardableHeaders()
    {
        Assert.DoesNotContain("Content-Type", HttpConstants.ExcludedHeaders);
        Assert.DoesNotContain("Authorization", HttpConstants.ExcludedHeaders);
        Assert.DoesNotContain("X-Llama-Model", HttpConstants.ExcludedHeaders);
    }
}
