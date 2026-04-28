using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LlamaDem.Config;

namespace LlamaDem.Middleware;

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RouterConfig _config;
    private readonly ILogger<BasicAuthMiddleware> _logger;

    public BasicAuthMiddleware(
        RequestDelegate next,
        IOptions<RouterConfig> config,
        ILogger<BasicAuthMiddleware> logger)
    {
        _next = next;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Missing or invalid Authorization header on {Path}", context.Request.Path);
            await SendUnauthorizedAsync(context);
            return;
        }

        var credentialBase64 = authHeader.Substring(6);
        string decodedCredentials;
        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(credentialBase64));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode Basic Auth credentials on {Path}", context.Request.Path);
            await SendUnauthorizedAsync(context);
            return;
        }

        var colonIndex = decodedCredentials.IndexOf(':');
        if (colonIndex <= 0)
        {
            _logger.LogWarning("Malformed Basic Auth credentials on {Path}", context.Request.Path);
            await SendUnauthorizedAsync(context);
            return;
        }

        var username = decodedCredentials[..colonIndex];
        var password = decodedCredentials[(colonIndex + 1)..];

        if (!CompareConstantTime(username, _config.AuthUsername) ||
            !CompareConstantTime(password, _config.AuthPassword))
        {
            _logger.LogWarning("Authentication failed for user '{User}' on {Path}", username, context.Request.Path);
            await SendUnauthorizedAsync(context);
            return;
        }

        _logger.LogDebug("Authentication successful for user '{User}' on {Path}", username, context.Request.Path);
        await _next(context);
    }

    private static bool CompareConstantTime(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    private static async Task SendUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"LlamaDem Router\", charset=\"UTF-8\"";
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", message = "Provide valid Basic Auth credentials." });
    }
}
