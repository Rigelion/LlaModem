namespace LlaModem.Services;

/// <summary>
/// Represents a successful API response with typed data.
/// </summary>
public sealed record SuccessResponse<T>(T Data) : ApiResult<T> where T : notnull;

/// <summary>
/// Represents an error API response with status code and error details.
/// </summary>
public sealed record ErrorResponse(int StatusCode, string Error, string Message) : ApiResult<object>;

/// <summary>
/// Discriminated union for API responses.
/// Follows the principle "Make invalid states hard to represent" by using types to enforce error handling.
/// </summary>
public interface ApiResult<T> where T : notnull;

/// <summary>
/// Builds API responses using a functional, side-effect-free style.
/// </summary>
public static class ApiResponseBuilder
{
    /// <summary>
    /// Creates a success response.
    /// </summary>
    public static SuccessResponse<T> Ok<T>(T data) where T : notnull => new(data);

    /// <summary>
    /// Creates a bad request error response.
    /// </summary>
    public static ErrorResponse BadRequest(string error, string message) => new(400, error, message);

    /// <summary>
    /// Creates a service unavailable error response.
    /// </summary>
    public static ErrorResponse ServiceUnavailable(string message) => new(503, "ServiceUnavailable", message);

    /// <summary>
    /// Writes an ApiResult to the HTTP context, handling success and error cases.
    /// </summary>
    public static async Task WriteAsync<T>(HttpContext context, ApiResult<T> result) where T : notnull
    {
        switch (result)
        {
            case SuccessResponse<T> success:
                await WriteSuccessAsync(context, success.Data);
                break;
            case ErrorResponse error:
                await WriteErrorAsync(context, error);
                break;
        }
    }

    private static async Task WriteSuccessAsync<T>(HttpContext context, T data) where T : notnull
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(data);
    }

    private static async Task WriteErrorAsync(HttpContext context, ErrorResponse error)
    {
        context.Response.StatusCode = error.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = error.Error, message = error.Message });
    }
}
