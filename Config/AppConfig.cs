namespace LlaModem.Config;

public class AppConfig
{
    public RouterConfig Router { get; set; } = new();
    public Dictionary<string, ModelConfig> Models { get; set; } = new();
}
