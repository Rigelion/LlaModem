namespace LlaModem.Services;

/// <summary>
/// Contract for routing proxy requests to model backends.
/// Encapsulates validation, lifecycle checks, and forwarding logic.
/// </summary>
public interface IModelProxyHandler
{
    /// <summary>
    /// Routes an incoming request to the appropriate model backend.
    /// Validates model header, ensures model is running, forwards request.
    /// </summary>
    Task RouteAsync(HttpContext context, CancellationToken ct = default);
}
