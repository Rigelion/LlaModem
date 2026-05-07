namespace LlaModem.Config;

/// <summary>
/// Configuration for usage statistics tracking via SQLite.
/// </summary>
public record UsageConfig
{
    public const string SectionName = "Usage";

    /// <summary>Whether to capture and persist token usage stats.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Path to the SQLite database file. Relative to app base directory.</summary>
    public string Path { get; init; } = "usage/usage.db";

    /// <summary>Whether to include timing data in usage records. Default: true.</summary>
    public bool IncludeTimings { get; init; } = true;
}
