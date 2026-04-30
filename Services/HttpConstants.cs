namespace LlaModem.Services;

/// <summary>
/// HTTP header names that should not be forwarded to backend servers.
/// These are hop-by-hop headers managed by the HTTP stack.
/// </summary>
public static class HttpConstants
{
    public static readonly HashSet<string> ExcludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade"
    };
}
