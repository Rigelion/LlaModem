namespace LlaModem.Models;

/// <summary>
/// A single usage entry for one request within a session (day).
/// </summary>
public record SessionEntry(
    DateTime Timestamp,
    string Model,
    string Route,
    TokenUsage Usage)
{
    public Timings? Timings => Usage.Timings;
}
