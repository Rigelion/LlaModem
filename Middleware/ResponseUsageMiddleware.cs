using LlaModem.Models;
using LlaModem.Services;

namespace LlaModem.Middleware;

/// <summary>
/// Combines response body logging and token usage capture into a single middleware.
/// Buffers the response body once, then logs the full body and extracts usage data
/// (tokens, timings) from llama-server JSON responses. The original response passes
/// through unchanged. Only processes non-streaming responses on proxy routes.
/// </summary>
public sealed class ResponseUsageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUsageService? _usageService;
    private readonly ModelMetricsService? _metricsService;
    private readonly ILogger<ResponseUsageMiddleware> _logger;

    public ResponseUsageMiddleware(
        RequestDelegate next,
        IUsageService? usageService,
        ModelMetricsService? metricsService,
        ILogger<ResponseUsageMiddleware> logger)
    {
        _next = next;
        _usageService = usageService;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Pure function: check if streaming
        if (UsageExtractor.IsStreaming(context))
        {
            await _next(context);
            return;
        }

        var requestTime = DateTimeOffset.UtcNow;
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            var responseTime = DateTimeOffset.UtcNow;
            buffer.Seek(0, SeekOrigin.Begin);

            // Log the full response body
            var bytes = buffer.ToArray();
            if (bytes.Length > 0)
            {
                var bodyString = System.Text.Encoding.UTF8.GetString(bytes);
                _logger.LogInformation("[RESPONSE BODY] {Method} {Path}\n{Body}", context.Request.Method, context.Request.Path, bodyString);
            }

            // Extract and record usage if this is a proxy route and usage service is available
            if (_usageService is not null)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isProxyRoute = path.StartsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase)
                                || path.StartsWith("/v1/completions", StringComparison.OrdinalIgnoreCase);

                if (isProxyRoute && !string.IsNullOrWhiteSpace(System.Text.Encoding.UTF8.GetString(bytes)))
                {
                    var raw = System.Text.Encoding.UTF8.GetString(bytes);
                    var usage = UsageExtractor.Extract(raw);

                    if (usage is not null)
                    {
                        var model = UsageExtractor.ExtractModel(raw) ?? "(unknown)";
                        var route = path;
                        var clientIp = context.Connection.RemoteIpAddress?.ToString();
                        var statusCode = context.Response.StatusCode;
                        var headers = UsageExtractor.CollectLlamaHeaders(context.Request.Headers);

                        var entry = new SessionEntry(
                            Timestamp: DateTimeOffset.UtcNow,
                            Model: model,
                            Route: route,
                            Usage: usage with
                            {
                                RequestTime = requestTime,
                                ResponseTime = responseTime
                            },
                            RequestTime: requestTime,
                            ResponseTime: responseTime,
                            ClientIp: clientIp,
                            StatusCode: statusCode,
                            RequestHeaders: headers);

                        _usageService.Record(entry);

                        var timingInfo = usage.Timings is { } t && (t.PromptMs.HasValue || t.CompletionMs.HasValue)
                            ? $", Prompt: {t.PromptMs:F1}ms, Completion: {t.CompletionMs:F1}ms"
                            : string.Empty;

                        _logger.LogInformation(
                            "[USAGE] {Model} {Route} — Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}{Timing}",
                            model, route, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens, timingInfo);

                        // Update metrics service
                        if (_metricsService is not null)
                        {
                            _metricsService.RecordUsage(model, usage.TotalTokens, entry.RequestTime);
                        }
                    }
                }
            }
        }
        catch
        {
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
            throw;
        }
        finally
        {
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
        }
    }
}
