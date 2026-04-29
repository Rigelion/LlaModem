using System.Collections.Generic;

namespace LlaModem.Config;

public class RouterConfig
{
    public string ListenUrl { get; set; } = "http://localhost:9000";
    public string AuthUsername { get; set; } = string.Empty;
    public string AuthPassword { get; set; } = string.Empty;
    public int IdleTimeoutSeconds { get; set; } = 600;
    public bool EnableBodyHeaderInjection { get; set; } = true;
    public Dictionary<string, string> BodyHeaderMappings { get; set; } = new();
}
