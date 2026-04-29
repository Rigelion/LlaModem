namespace LlaModem.Utilities;

/// <summary>
/// Shared extension methods for HttpRequest operations.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Reads the request body into a byte array, enabling buffering and resetting the stream position.
    /// </summary>
    public static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();

        byte[] buffer;
        using (var ms = new MemoryStream())
        {
            await request.Body.CopyToAsync(ms);
            buffer = ms.ToArray();
        }

        request.Body.Position = 0;
        return buffer;
    }
}
