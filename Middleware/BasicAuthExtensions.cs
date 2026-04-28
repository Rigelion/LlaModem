using LlamaDem.Middleware;

namespace Microsoft.AspNetCore.Builder;

public static class BasicAuthExtensions
{
    /// <summary>
    /// Conditionally applies BasicAuthMiddleware to requests matching the given path pattern.
    /// </summary>
    public static IApplicationBuilder UseBasicAuthWhen(this IApplicationBuilder app, string pathStartsWith)
    {
        return app.UseWhen(
            context => context.Request.Path.StartsWithSegments(pathStartsWith),
            pipeline => pipeline.UseMiddleware<BasicAuthMiddleware>());
    }
}
