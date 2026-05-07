namespace LlaModem.Config;

/// <summary>
/// Configuration for usage statistics tracking.
/// </summary>
public class UsageConfig
{
    public const string SectionName = "Usage";

    /// <summary>Whether to capture and persist token usage stats.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Directory to store daily usage markdown files. Relative to app base directory.</summary>
    public string Path { get; set; } = "usage";

    /// <summary>Filename pattern, e.g. "usage-{date}.md". {date} is replaced with yyyy-MM-dd.</summary>
    public string FilenamePattern { get; set; } = "usage-{date}.md";

    /// <summary>Whether to include timing data in usage reports. Default: true.</summary>
    public bool IncludeTimings { get; set; } = true;
}
