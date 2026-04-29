using LlaModem.Config;
using LlaModem.Utilities;

namespace LlaModem.Services;

public class RequestForwarder : IRequestForwarder
{
    private readonly IHeaderValueInjector _headerValueInjector;
    private readonly ILogger<RequestForwarder> _logger;

    public RequestForwarder(
        IHeaderValueInjector headerValueInjector,
        ILogger<RequestForwarder> logger)
    {
        _headerValueInjector = headerValueInjector;
        _logger = logger;
    }

    public async Task ForwardAsync(
        HttpContext context,
        HttpRequest request,
        HttpClient httpClient,
        string targetUrl)
    {
        // Inject configured header values into the JSON request body
        await _headerValueInjector.InjectAsync(context, _logger);

        // Explicitly capture the (possibly modified) body so forwarding is independent of middleware ordering
        var buffer = await HttpRequestExtensions.ReadBodyAsync(request);

        var method = System.Net.Http.HttpMethod.Parse(request.Method);
        var forwardedRequest = new HttpRequestMessage(method, targetUrl);

        foreach (var header in request.Headers)
        {
            if (EndpointSetup.ExcludedHeaders.Contains(header.Key))
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
                if (EndpointSetup.ExcludedHeaders.Contains(header.Key))
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

    public static string BuildTargetUrl(ModelConfig modelConfig, HttpRequest request)
    {
        var backendUrl = modelConfig.BackendUrl.TrimEnd('/');
        var path = request.Path.Value!;
        var targetPath = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ? path[3..] : path;
        var targetUrl = $"{backendUrl}{targetPath}";
        if (request.QueryString.HasValue)
            targetUrl += request.QueryString.Value;
        return targetUrl;
    }
}
