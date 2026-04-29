namespace LlaModem.Config;

/// <summary>
/// Root application configuration bound from "Models" section.
/// Router settings are configured separately via RouterConfig.
/// </summary>
public class AppConfig
{
    public Dictionary<string, ModelConfig> Models { get; set; } = new();
}
