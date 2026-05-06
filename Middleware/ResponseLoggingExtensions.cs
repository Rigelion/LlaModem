namespace LlaModem.Middleware;

public static class ResponseLoggingExtensions
{
    public static IApplicationBuilder UseResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ResponseLoggingMiddleware>();
    }
}
