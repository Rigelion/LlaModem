using LlaModem.Models;
using System.Text;
using System.Text.Json;

namespace LlaModem.Services;

/// <summary>
/// Pure function for extracting token usage data from JSON responses.
/// No side effects — takes raw JSON string, returns optional usage data.
/// </summary>
public static class UsageExtractor
{
    /// <summary>
    /// Extract token usage from raw JSON response string.
    /// Handles both single JSON objects and NDJSON/SSE formats.
    /// </summary>
    public static TokenUsage? Extract(string raw)
    {
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

    /// <summary>
    /// Extract model name from raw JSON response string.
    /// </summary>
    public static string? ExtractModel(string raw)
    {
        var cleaned = StripSseFormat(raw);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return GetString(doc.RootElement, "model");
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
                    var model = GetString(doc.RootElement, "model");
                    if (!string.IsNullOrEmpty(model))
                        return model;
                }
                catch { /* skip */ }
            }
        }

        return null;
    }

    /// <summary>
    /// Collect X-Llama headers from request headers into a serialized dictionary.
    /// </summary>
    public static string? CollectLlamaHeaders(IHeaderDictionary headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
        {
            if (key.StartsWith("X-Llama", StringComparison.OrdinalIgnoreCase))
                dict[key] = string.Join("; ", value!);
        }
        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }

    /// <summary>
    /// Determine if response is streaming based on content type and transfer encoding.
    /// </summary>
    public static bool IsStreaming(HttpContext context)
    {
        var contentType = context.Response.Headers.ContentType.ToString();
        return contentType.Contains("stream", StringComparison.OrdinalIgnoreCase)
            || context.Response.Headers.TransferEncoding.Any();
    }

    private static string StripSseFormat(string raw)
    {
        var sb = new StringBuilder(raw.Length);
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

        var requestId = GetString(root, "id");
        var created = TryExtractCreated(root);
        var cachedTokens = TryExtractCachedTokens(root);

        var timings = TryExtractTimings(root);

        return new TokenUsage(
            promptTokens,
            completionTokens,
            totalTokens,
            timings.PromptMs,
            timings.CompletionMs,
            timings.PromptPerTokenMs,
            timings.CompletionPerTokenMs,
            timings.CacheHits,
            RequestId: requestId,
            Created: created,
            CachedTokens: cachedTokens);
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

    private static DateTimeOffset? TryExtractCreated(JsonElement root)
    {
        if (!root.TryGetProperty("created", out var value) || value.ValueKind != JsonValueKind.Number)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value.GetInt64());
        }
        catch
        {
            return null;
        }
    }

    private static int? TryExtractCachedTokens(JsonElement root)
    {
        if (!root.TryGetProperty("prompt_tokens_details", out var details) || details.ValueKind != JsonValueKind.Object)
            return null;

        if (!details.TryGetProperty("cached_tokens", out var value) || value.ValueKind != JsonValueKind.Number)
            return null;

        return value.GetInt32();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }
}
