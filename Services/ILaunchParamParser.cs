namespace LlaModem.Services;

/// <summary>
/// Parses optional model launch parameters from HTTP headers.
/// </summary>
public interface ILaunchParamParser
{
    /// <summary>
    /// Parses temperature, top_p, and presence_penalty from request headers.
    /// Returns null if no launch params are present.
    /// </summary>
    Task<ModelLaunchParams?> ParseAsync(HttpContext context, HttpRequest request);
}
