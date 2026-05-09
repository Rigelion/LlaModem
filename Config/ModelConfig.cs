namespace LlaModem.Config;

/// <summary>
/// Configuration for a single model.
/// Environment variables in StartScript are expanded at binding time in Program.cs.
/// </summary>
public record ModelConfig
{
    public string StartScript { get; set; } = string.Empty;
}
