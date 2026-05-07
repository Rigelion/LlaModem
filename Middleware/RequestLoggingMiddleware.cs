using LlaModem.Config;
using LlaModem.Utilities;

namespace LlaModem.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var temperature = context.Request.Headers[ProxyHeaders.Temperature].FirstOrDefault();
        var topP = context.Request.Headers[ProxyHeaders.TopP].FirstOrDefault();
        var presencePenalty = context.Request.Headers[ProxyHeaders.PresencePenalty].FirstOrDefault();
        var hasAuth = context.Request.Headers.ContainsKey("Authorization") ? "yes" : "no";

        _logger.LogInformation(
            "[REQUEST] {Method} {Scheme}://{Host}{Path}{Query} | Client: {ClientIp} | Model: {Model} | Temp: {Temperature} | TopP: {TopP} | PP: {PresencePenalty} | Auth: {HasAuth}",
            method, scheme, context.Request.Host, path, queryString, clientIp, modelName,
            temperature ?? "(default)", topP ?? "(default)", presencePenalty ?? "(default)", hasAuth);

        // Log full request body at Debug level
        if (context.Request.Body.CanRead)
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
            var responseSize = context.Response.Body.Length;

            _logger.LogInformation(
                "[RESPONSE] {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | ResponseSize: {ResponseSize} bytes",
                method, path, statusCode, sw.ElapsedMilliseconds, responseSize);
        }
    }
}
