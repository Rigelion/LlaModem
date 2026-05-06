namespace LlaModem.Middleware;

public class ResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseLoggingMiddleware> _logger;

    public ResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<ResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isStreaming = context.Response.Headers.ContentType.ToString().Contains("stream", StringComparison.OrdinalIgnoreCase)
                       || context.Response.Headers.TransferEncoding.Any();

        // Only capture non-streaming responses
        if (!isStreaming)
        {
            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);

                buffer.Seek(0, SeekOrigin.Begin);
                var bytes = buffer.ToArray();
                if (bytes.Length > 0)
                {
                    var bodyString = System.Text.Encoding.UTF8.GetString(bytes);
                    _logger.LogInformation("[RESPONSE BODY] {Method} {Path}\n{Body}", context.Request.Method, context.Request.Path, bodyString);
                }

                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
            }
            catch
            {
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                throw;
            }
        }
        else
        {
            await _next(context);
        }
    }
}
