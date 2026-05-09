using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Shared utility for writing JSON error responses.
/// </summary>
public static class ErrorResponseWriter
{
    public static async Task WriteAsync(HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error, message });
    }

    /// <summary>
    /// Creates a standardized error response object.
    /// </summary>
    public static ErrorModel CreateError(int statusCode, string error, string message) => new(error, message);

    /// <summary>
    /// Writes a standardized error response with optional logging.
    /// </summary>
    public static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string error,
        string message,
        ILogger? logger = null,
        Exception? exception = null)
    {
        logger?.LogError(exception, "Error response: {Error} - {Message}", error, message);
        await WriteAsync(context, statusCode, error, message);
    }
}

/// <summary>
/// Standardized error response model.
/// </summary>
public sealed record ErrorModel(string Error, string Message);
