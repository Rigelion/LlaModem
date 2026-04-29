using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace LlaModem.Services;

public interface IHeaderValueInjector
{
    /// <summary>
    /// Injects configured header values into the request body at root level.
    /// Modifies the body stream in-place and resets position to 0.
    /// Only processes application/json bodies with a parseable JSON object.
    /// </summary>
    void Inject(HttpContext context, ILogger logger);
}

public class HeaderValueInjector : IHeaderValueInjector
{
    private readonly bool _enabled;
    private readonly Dictionary<string, string> _mappings;

    public HeaderValueInjector(bool enabled, Dictionary<string, string> mappings)
    {
        _enabled = enabled;
        _mappings = mappings;
    }

    public void Inject(HttpContext context, ILogger logger)
    {
        // Feature switch
        if (!_enabled)
            return;

        // Only process JSON requests
        var contentType = context.Request.ContentType;
        if (contentType == null || !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return;

        // Check if there are any configured mappings
        if (_mappings.Count == 0)
            return;

        // Read the body (already buffered by RequestLoggingMiddleware)
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var bodyText = reader.ReadToEnd();
        context.Request.Body.Position = 0;

        // Try to parse as JSON object
        var parsedNode = JsonNode.Parse(bodyText);
        if (parsedNode is not JsonObject jsonObject)
            return; // Not a valid JSON object, skip injection

        // Inject header values at root level
        var hasInjection = false;

        foreach (var (headerName, bodyKey) in _mappings)
        {
            var headerValue = context.Request.Headers[headerName].FirstOrDefault();
            if (string.IsNullOrEmpty(headerValue))
                continue;

            // Auto-convert value type and inject
            var convertedValue = ConvertHeaderValue(headerValue);
            var valueType = GetValueTypeName(convertedValue);

            jsonObject[bodyKey] = convertedValue;
            hasInjection = true;

            logger.LogInformation(
                "[HEADER_INJECT] {Header} -> {Key}={Value}({Type})",
                headerName, bodyKey, headerValue, valueType);
        }

        if (!hasInjection)
            return;

        // Serialize back to JSON and write to body stream
        var modifiedJson = jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var modifiedBytes = System.Text.Encoding.UTF8.GetBytes(modifiedJson);
        context.Request.Body.Position = 0;
        context.Request.Body.Write(modifiedBytes, 0, modifiedBytes.Length);
        context.Request.Body.SetLength(modifiedBytes.LongLength);
    }

    private static JsonNode ConvertHeaderValue(string value)
    {
        // Try boolean first (case-insensitive)
        if (bool.TryParse(value, out var boolVal))
            return boolVal;

        // Try integer
        if (int.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var intVal))
            return intVal;

        // Try double
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
            return doubleVal;

        // Default to string
        return value;
    }

    private static string GetValueTypeName(JsonNode node)
    {
        var kind = node.GetValueKind();
        return kind switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => "number",
            _ => "string"
        };
    }
}
