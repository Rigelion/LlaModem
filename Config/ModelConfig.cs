namespace LlaModem.Config;

/// <summary>
/// Configuration for a single model.
/// </summary>
public record ModelConfig
{
    public string StartScript { get; set; } = string.Empty;
    public string? BackendUrl { get; set; }  // Optional, defaults to global BackendUrl
}
