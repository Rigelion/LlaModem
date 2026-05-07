using LlaModem.Models;
using LlaModem.Services;
using System.Text.Json;

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
    private readonly ILogger<ResponseUsageMiddleware> _logger;

    public ResponseUsageMiddleware(
        RequestDelegate next,
        IUsageService? usageService,
        ILogger<ResponseUsageMiddleware> logger)
    {
        _next = next;
        _usageService = usageService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isStreaming = IsStreamingResponse(context);

        // Only capture non-streaming responses
        if (isStreaming)
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

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
                    var usage = TryExtractUsageFromRaw(raw);

                    if (usage is not null)
                    {
                        var model = TryExtractModel(raw) ?? "(unknown)";
                        var route = path;

                        _usageService.Record(new SessionEntry(
                            DateTimeOffset.UtcNow,
                            model,
                            route,
                            usage));

                        var timingInfo = usage.Timings is { } t && (t.PromptMs.HasValue || t.CompletionMs.HasValue)
                            ? $", Prompt: {t.PromptMs:F1}ms, Completion: {t.CompletionMs:F1}ms"
                            : string.Empty;

                        _logger.LogInformation(
                            "[USAGE] {Model} {Route} — Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}{Timing}",
                            model, route, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens, timingInfo);
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

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody);
    }

    private static bool IsStreamingResponse(HttpContext context)
    {
        var contentType = context.Response.Headers.ContentType.ToString();
        return contentType.Contains("stream", StringComparison.OrdinalIgnoreCase)
            || context.Response.Headers.TransferEncoding.Any();
    }

    private static TokenUsage? TryExtractUsageFromRaw(string raw)
    {
        // Strip SSE data: prefix if present (llama-server SSE format)
        var cleaned = StripSseFormat(raw);

        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        // Try parsing as single JSON object first (standard OpenAI response)
        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return TryExtractUsage(doc.RootElement);
        }
        catch
        {
            // Fall through to NDJSON parsing
        }

        // Parse as NDJSON — one JSON object per line
        var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        TokenUsage? lastUsage = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "[DONE]" || trimmed.Length == 0)
                continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var usage = TryExtractUsage(doc.RootElement);
                if (usage is not null)
                    lastUsage = usage;
            }
            catch
            {
                // Skip unparseable lines
            }
        }

        return lastUsage;
    }

    private static string? TryExtractModel(string raw)
    {
        var cleaned = StripSseFormat(raw);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return TryGetString(doc.RootElement, "model");
        }
        catch
        {
            // NDJSON fallback — check each line
            var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (trimmed == "[DONE]" || trimmed.Length == 0)
                    continue;
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var model = TryGetString(doc.RootElement, "model");
                    if (!string.IsNullOrEmpty(model))
                        return model;
                }
                catch { /* skip */ }
            }
        }

        return null;
    }

    private static string StripSseFormat(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("data: ", StringComparison.Ordinal))
                sb.AppendLine(trimmed[6..]);
            else if (trimmed.Length > 0)
                sb.AppendLine(trimmed);
        }

        return sb.ToString().Trim();
    }

    private static TokenUsage? TryExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            return null;

        var promptTokens = TryGetInt32(usageElement, "prompt_tokens");
        var completionTokens = TryGetInt32(usageElement, "completion_tokens");
        var totalTokens = TryGetInt32(usageElement, "total_tokens");

        var timings = TryExtractTimings(root);

        return new TokenUsage(
            promptTokens,
            completionTokens,
            totalTokens,
            timings.PromptMs,
            timings.CompletionMs,
            timings.PromptPerTokenMs,
            timings.CompletionPerTokenMs,
            timings.CacheHits);
    }

    private static Timings TryExtractTimings(JsonElement root)
    {
        if (!root.TryGetProperty("timings", out var timingsElement) || timingsElement.ValueKind != JsonValueKind.Object)
            return new Timings();

        return new Timings(
            TryGetDouble(timingsElement, "prompt_ms"),
            TryGetDouble(timingsElement, "predicted_ms"),
            TryGetDouble(timingsElement, "prompt_per_token_ms"),
            TryGetDouble(timingsElement, "predicted_per_token_ms"),
            TryGetInt32Nullable(timingsElement, "cache_n"));
    }

    private static int TryGetInt32(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();
        return 0;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetDouble();
        return null;
    }

    private static int? TryGetInt32Nullable(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();
        return null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }
}
