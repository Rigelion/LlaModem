namespace LlaModem.Services;

/// <summary>
/// Contract for forwarding HTTP requests to backend services.
/// Enables mocking in tests and potential implementation swaps.
/// </summary>
public interface IRequestForwarder
{
    /// <summary>
    /// Forwards the incoming request to the specified backend URL.
    /// </summary>
    Task ForwardAsync(HttpContext context, string backendUrl, CancellationToken ct = default);
}
