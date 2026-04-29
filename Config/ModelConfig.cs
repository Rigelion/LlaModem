namespace LlaModem.Config;

/// <summary>
/// Configuration for a single model backend.
/// Environment variables in StartScript are expanded at assignment time.
/// </summary>
public class ModelConfig
{
    private string _startScript = string.Empty;

    public string StartScript
    {
        get => _startScript;
        set => _startScript = Environment.ExpandEnvironmentVariables(value);
    }

    public string BackendUrl { get; set; } = string.Empty;
}
