namespace LlaModem.Config;

/// <summary>
/// Root application configuration bound from the root section.
/// Router settings are configured separately via RouterConfig.
/// </summary>
public record AppConfig
{
    public string BackendUrl { get; init; } = string.Empty;
    public Dictionary<string, ModelConfig> Models { get; init; } = new();
}
