using LlaModem.Services;
using Microsoft.AspNetCore.Http;

namespace LlaModem.Tests;

public class RequestForwarderTests
{
    [Fact]
    public void BuildTargetUrl_StripsV1Prefix()
    {
        var request = CreateRequest("/v1/chat/completions");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001", request);

        Assert.Equal("http://localhost:8001/chat/completions", result);
    }

    [Fact]
    public void BuildTargetUrl_PreservesQueryString()
    {
        var request = CreateRequest("/v1/chat/completions?foo=bar");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001", request);

        Assert.Equal("http://localhost:8001/chat/completions?foo=bar", result);
    }

    [Fact]
    public void BuildTargetUrl_HandlesTrailingSlashOnBackendUrl()
    {
        var request = CreateRequest("/v1/completions");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001/", request);

        Assert.Equal("http://localhost:8001/completions", result);
    }

    [Fact]
    public void BuildTargetUrl_NonV1Path_UsedAsIs()
    {
        var request = CreateRequest("/health");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001", request);

        Assert.Equal("http://localhost:8001/health", result);
    }

    [Fact]
    public void BuildTargetUrl_DeepPath_PreservesAllSegments()
    {
        var request = CreateRequest("/v1/models/ada/embeddings");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001", request);

        Assert.Equal("http://localhost:8001/models/ada/embeddings", result);
    }

    [Fact]
    public void BuildTargetUrl_CaseInsensitiveV1Check()
    {
        var request = CreateRequest("/V1/chat/completions");

        var result = RequestForwarder.BuildTargetUrl("http://localhost:8001", request);

        Assert.Equal("http://localhost:8001/chat/completions", result);
    }

    private static HttpRequest CreateRequest(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        return httpContext.Request;
    }
}
