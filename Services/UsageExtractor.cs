using LlaModem.Models;
using LlaModem.Utilities;
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
        return ParseSingleOrNdjson(cleaned);
    }

    private static TokenUsage? ParseSingleOrNdjson(string cleaned)
    {
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
        var usages = new List<TokenUsage>();
        foreach (var usage in lines.Select(ParseLine))
        {
            if (usage is not null)
                usages.Add(usage);
        }
        return usages.Count > 0 ? usages[^1] : null;
    }

    private static TokenUsage? ParseLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed == "[DONE]" || trimmed.Length == 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return TryExtractUsage(doc.RootElement);
        }
        catch
        {
            // Skip unparseable lines
            return null;
        }
    }

    /// <summary>
    /// Extract model name from raw JSON response string.
    /// </summary>
    public static string? ExtractModel(string raw)
    {
        var cleaned = StripSseFormat(raw);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;
        return ExtractModelFromJson(cleaned);
    }

    private static string? ExtractModelFromJson(string cleaned)
    {
        // Try parsing as single JSON object first
        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return GetString(doc.RootElement, "model");
        }
        catch
        {
            // NDJSON fallback — check each line in reverse order (last wins)
            var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var models = new List<string>();
            foreach (var model in lines.Reverse().Select(ExtractModelFromLine))
            {
                if (model is not null)
                    models.Add(model);
            }
            return models.Count > 0 ? models[^1] : null;
        }
    }

    private static string? ExtractModelFromLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed == "[DONE]" || trimmed.Length == 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var model = GetString(doc.RootElement, "model");
            return string.IsNullOrEmpty(model) ? null : model;
        }
        catch
        {
            return null;
        }
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

    /// <summary>
    /// Strips Server-Sent Events (SSE) format from raw JSON.
    /// Removes "data: " prefixes and filters empty lines.
    /// </summary>
    private static string StripSseFormat(string raw)
    {
        return string.Join("\n", raw.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("data: ", StringComparison.Ordinal))
            .Concat(raw.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                .Where(line => line.Trim().StartsWith("data: ", StringComparison.Ordinal))
                .Select(line => line.Trim()[6..]))
            .Where(line => line.Length > 0));
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
