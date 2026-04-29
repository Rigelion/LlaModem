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
        var modelName = context.Request.Headers["X-Llama-Model"].FirstOrDefault() ?? "(none)";
        var hasAuth = context.Request.Headers.ContainsKey("Authorization") ? "yes" : "no";

        _logger.LogInformation(
            "[REQUEST] {Method} {Scheme}://{Host}{Path}{Query} | Client: {ClientIp} | Model: {Model} | Auth: {HasAuth}",
            method, scheme, context.Request.Host, path, queryString, clientIp, modelName, hasAuth);

        // Log full request body at Debug level
        if (context.Request.Body.CanRead)
        {
            var buffer = await ReadRequestBodyAsync(context.Request);
            if (buffer.Length > 0)
            {
                var bodyString = System.Text.Encoding.UTF8.GetString(buffer);
                _logger.LogDebug("[REQUEST BODY] {Body}", bodyString);
            }
        }

        // Capture the response body for logging status after response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

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
            var responseSize = responseBody.Length;

            _logger.LogInformation(
                "[RESPONSE] {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | ResponseSize: {ResponseSize} bytes",
                method, path, statusCode, sw.ElapsedMilliseconds, responseSize);

            // Copy the response body back to the original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private static async Task<byte[]> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();

        byte[] buffer;
        using (var ms = new MemoryStream())
        {
            await request.Body.CopyToAsync(ms);
            buffer = ms.ToArray();
        }

        // Reset the position so downstream middleware/endpoints can read the body
        request.Body.Position = 0;

        return buffer;
    }
}
