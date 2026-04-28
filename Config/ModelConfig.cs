namespace LlaModem.Config;

public class ModelConfig
{
    private string _startScript = string.Empty;

    public string StartScript
    {
        get => Environment.ExpandEnvironmentVariables(_startScript);
        set => _startScript = value;
    }
    public string BackendUrl { get; set; } = string.Empty;
}
