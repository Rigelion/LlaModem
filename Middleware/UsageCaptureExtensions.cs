namespace LlaModem.Middleware;

public static class UsageCaptureExtensions
{
    public static IApplicationBuilder UseUsageCapture(this IApplicationBuilder app)
    {
        return app.UseMiddleware<UsageCaptureMiddleware>();
    }
}
