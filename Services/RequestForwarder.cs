using LlaModem.Config;
using LlaModem.Utilities;

namespace LlaModem.Services;

public sealed class RequestForwarder : IRequestForwarder
{
    private readonly ILogger<RequestForwarder> _logger;

    public RequestForwarder(ILogger<RequestForwarder> logger)
    {
        _logger = logger;
    }

    public async Task ForwardAsync(
        HttpContext context,
        string backendUrl,
        CancellationToken ct = default)
    {
        var request = context.Request;
        using var httpClient = new HttpClient();
        await ForwardAsync(context, request, httpClient, backendUrl);
    }

    public async Task ForwardAsync(
        HttpContext context,
        HttpRequest request,
        HttpClient httpClient,
        string targetUrl)
    {
        // Explicitly capture the request body so forwarding is independent of middleware ordering
        var buffer = await HttpRequestExtensions.ReadBodyAsync(request);

        var method = System.Net.Http.HttpMethod.Parse(request.Method);
        var forwardedRequest = new HttpRequestMessage(method, targetUrl);

        foreach (var header in request.Headers)
        {
            if (HttpConstants.ExcludedHeaders.Contains(header.Key))
                continue;
            forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        }

        if (buffer.Length > 0)
        {
            forwardedRequest.Content = new ByteArrayContent(buffer);
            foreach (var header in request.Headers)
            {
                if (header.Key is "Content-Length")
                    continue;
                if (HttpConstants.ExcludedHeaders.Contains(header.Key))
                    continue;
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    forwardedRequest.Content!.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
                }
            }
        }

        var response = await httpClient.SendAsync(
            forwardedRequest,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        foreach (var header in response.Headers)
        {
            if (header.Key is "Transfer-Encoding")
                continue;
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.StatusCode = (int)response.StatusCode;
        await response.Content.CopyToAsync(context.Response.Body);
    }

    public static string BuildTargetUrl(string backendUrl, HttpRequest request)
    {
        var url = backendUrl.TrimEnd('/');
        var path = request.Path.Value!;
        var targetPath = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ? path[3..] : path;
        var targetUrl = $"{url}{targetPath}";
        if (request.QueryString.HasValue)
            targetUrl += request.QueryString.Value;
        return targetUrl;
    }
}
