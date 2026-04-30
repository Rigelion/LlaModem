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
}
