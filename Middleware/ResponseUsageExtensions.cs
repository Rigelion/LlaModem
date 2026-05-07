using LlaModem.Services;

namespace LlaModem.Middleware;

public static class ResponseUsageExtensions
{
    /// <summary>
    /// Combines response body logging and token usage capture into a single middleware.
    /// Buffers the response body once, then logs the full body and extracts usage data.
    /// </summary>
    public static IApplicationBuilder UseResponseUsageCapture(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ResponseUsageMiddleware>();
    }
}
