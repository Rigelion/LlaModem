namespace LlaModem.Services;

/// <summary>
/// Forwards HTTP requests to a backend URL and copies the response back.
/// </summary>
public interface IRequestForwarder
{
    /// <summary>
    /// Forwards the request to the target URL and writes the response back to the context.
    /// </summary>
    Task ForwardAsync(HttpContext context, HttpRequest request, HttpClient httpClient, string targetUrl);
}
