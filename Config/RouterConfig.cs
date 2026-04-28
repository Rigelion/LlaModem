namespace LlamaDem.Config;

public class RouterConfig
{
    public string ListenUrl { get; set; } = "http://localhost:9000";
    public string AuthUsername { get; set; } = string.Empty;
    public string AuthPassword { get; set; } = string.Empty;
    public int IdleTimeoutSeconds { get; set; } = 600;
}
