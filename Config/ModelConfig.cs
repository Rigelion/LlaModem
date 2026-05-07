namespace LlaModem.Config;

/// <summary>
/// Configuration for a single model backend.
/// Environment variables in StartScript are expanded at assignment time.
/// </summary>
public record ModelConfig
{
    private readonly string? _expandedScript;

    public string StartScript
    {
        get => _expandedScript ?? string.Empty;
        init => _expandedScript = Environment.ExpandEnvironmentVariables(value ?? string.Empty);
    }

    public string BackendUrl { get; init; } = string.Empty;
}
