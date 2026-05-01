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

        var isStreaming = context.Response.Headers.ContentType.ToString().Contains("stream", StringComparison.OrdinalIgnoreCase)
                       || context.Response.Headers.TransferEncoding.Any();

        // Only capture usage from non-streaming responses
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

    private async Task ExtractAndRecordUsage(HttpContext context, MemoryStream bodyStream, Stream originalBody)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(bodyStream.ToArray());

            if (string.IsNullOrWhiteSpace(json))
            {
                await WriteBufferedBody(bodyStream, originalBody);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract usage object — supports both OpenAI and Ollama response shapes
            var usage = TryExtractUsage(root);

            if (usage is not null)
            {
                var model = TryGetString(root, "model") ?? "(unknown)";
                var route = context.Request.Path.Value ?? context.Request.Path.ToString();

                _usageService.Record(new SessionEntry(
                    DateTime.Now,
                    model,
                    route,
                    usage));

                _logger.LogDebug(
                    "[USAGE] {Model} {Route} — Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                    model, route, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
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

    private static TokenUsage? TryExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            return null;

        var promptTokens = TryGetInt32(usageElement, "prompt_tokens");
        var completionTokens = TryGetInt32(usageElement, "completion_tokens");
        var totalTokens = TryGetInt32(usageElement, "total_tokens");

        return new TokenUsage(promptTokens, completionTokens, totalTokens);
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

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }
}


