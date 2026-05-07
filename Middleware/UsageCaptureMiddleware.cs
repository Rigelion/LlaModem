using LlaModem.Models;
using LlaModem.Services;
using System.Text.Json;

namespace LlaModem.Middleware;

/// <summary>
/// Intercepts non-streaming responses to extract token usage stats
/// and persist them via IUsageService. The original response passes through unchanged.
/// </summary>
public sealed class UsageCaptureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUsageService _usageService;
    private readonly ILogger<UsageCaptureMiddleware> _logger;

    public UsageCaptureMiddleware(
        RequestDelegate next,
        IUsageService usageService,
        ILogger<UsageCaptureMiddleware> logger)
    {
        _next = next;
        _usageService = usageService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only capture usage from proxy routes that return token stats
        var path = context.Request.Path.Value ?? string.Empty;
        var isProxyRoute = path.StartsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/v1/completions", StringComparison.OrdinalIgnoreCase);

        if (!isProxyRoute)
        {
            await _next(context);
            return;
        }

        // Detect streaming from the request body (stream: true) — response headers aren't set yet at this point
        var isStreaming = await IsStreamingRequestAsync(context.Request);

        if (!isStreaming)
        {
            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);

                buffer.Seek(0, SeekOrigin.Begin);
                await ExtractAndRecordUsage(context, buffer, originalBody);
            }
            catch
            {
                await WriteBufferedBody(buffer, originalBody);
                throw;
            }
        }
        else
        {
            await _next(context);
        }
    }

    private static async Task<bool> IsStreamingRequestAsync(HttpRequest request)
    {
        if (request.Body is null || !request.Body.CanRead)
            return false;

        if (request.Body.CanSeek)
        {
            if (request.Body.Length == 0)
                return false;
        }
        else
        {
            // PipeStream (default ASP.NET Core body) doesn't support Length.
            // Read all content, then replace Body with a seekable MemoryStream.
            var content = await ReadAllBytesAsync(request.Body);
            if (content.Length == 0)
                return false;

            request.Body = new MemoryStream(content);
        }

        var originalPosition = request.Body.Position;
        try
        {
            using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            // Check for "stream": true in the JSON body
            if (body.Contains("\"stream\"", StringComparison.OrdinalIgnoreCase)
                && body.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                // Verify it's actually stream:true, not some other field named stream
                var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("stream", out var streamProp) && streamProp.GetBoolean())
                    return true;
            }

            return false;
        }
        finally
        {
            request.Body.Position = originalPosition;
        }
    }

    private async Task ExtractAndRecordUsage(HttpContext context, MemoryStream bodyStream, Stream originalBody)
    {
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(bodyStream.ToArray());

            if (string.IsNullOrWhiteSpace(raw))
            {
                await WriteBufferedBody(bodyStream, originalBody);
                return;
            }

            // Try to find usage — handle standard JSON, SSE (data: prefix), and NDJSON
            var usage = TryExtractUsageFromRaw(raw);

            if (usage is not null)
            {
                var model = TryExtractModel(raw) ?? "(unknown)";
                var route = context.Request.Path.Value ?? context.Request.Path.ToString();

                _usageService.Record(new SessionEntry(
                    DateTime.Now,
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[USAGE] Failed to extract token usage from response");
        }
        finally
        {
            await WriteBufferedBody(bodyStream, originalBody);
        }
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
        // Handle SSE format: "data: {...}\n\ndata: {...}\n\ndata: [DONE]"
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

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static async Task WriteBufferedBody(MemoryStream bodyStream, Stream originalBody)
    {
        bodyStream.Seek(0, SeekOrigin.Begin);
        await bodyStream.CopyToAsync(originalBody);
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


