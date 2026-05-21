using LlaModem.Config;
using LlaModem.Utilities;
using Microsoft.Extensions.Options;

namespace LlaModem.Middleware;

/// <summary>
/// Logs request and response details with configurable body logging.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly bool _logRequestBody;
    private readonly bool _logResponseBody;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<RouterConfig> routerConfig)
    {
        _next = next;
        _logger = logger;
        var logging = routerConfig.Value?.Logging ?? new RouterConfig.LoggingConfig();
        _logRequestBody = logging.LogRequestBody;
        _logResponseBody = logging.LogResponseBody;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Log request details
        var method = context.Request.Method;
        var scheme = context.Request.Scheme;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var modelName = context.Request.Headers[ProxyHeaders.Model].FirstOrDefault() ?? "(none)";
        var hasAuth = context.Request.Headers.ContainsKey("Authorization") ? "yes" : "no";

        _logger.LogInformation(
            "[REQUEST] {Method} {Scheme}://{Host}{Path}{Query} | Client: {ClientIp} | Model: {Model} | Auth: {HasAuth}",
            method, scheme, context.Request.Host, path, queryString, clientIp, modelName,
            hasAuth);

        // Log full request body at Debug level (configurable)
        if (_logRequestBody && context.Request.Body.CanRead)
        {
            var buffer = await HttpRequestExtensions.ReadBodyAsync(context.Request);
            if (buffer.Length > 0)
            {
                var bodyString = System.Text.Encoding.UTF8.GetString(buffer);
                _logger.LogDebug("[REQUEST BODY] {Body}", bodyString);
            }
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REQUEST] {Method} {Path} — unhandled exception", method, path);
            throw;
        }
        finally
        {
            sw.Stop();

            var statusCode = context.Response.StatusCode;
            var responseSize = context.Response.Body.CanSeek
                ? context.Response.Body.Length
                : -1L;

            _logger.LogInformation(
                "[RESPONSE] {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | ResponseSize: {ResponseSize} bytes",
                method, path, statusCode, sw.ElapsedMilliseconds, responseSize);

            // Log response body at Debug level (configurable)
            if (_logResponseBody && context.Response.Body.CanSeek && context.Response.Body.Length > 0)
            {
                try
                {
                    context.Response.Body.Position = 0;
                    var buffer = new byte[context.Response.Body.Length];
                    var bytesRead = context.Response.Body.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var bodyString = System.Text.Encoding.UTF8.GetString(buffer);
                        _logger.LogDebug("[RESPONSE BODY] {Body}", bodyString);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't throw - response logging is non-critical
                    _logger.LogWarning(ex, "[RESPONSE BODY] Failed to log response body");
                }
            }
        }
    }
}
