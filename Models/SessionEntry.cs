namespace LlaModem.Models;

/// <summary>
/// A single usage entry for one request within a session (day).
/// </summary>
public record SessionEntry(
    DateTimeOffset Timestamp,
    string Model,
    string Route,
    TokenUsage Usage,
    DateTimeOffset RequestTime,
    DateTimeOffset ResponseTime,
    string? ClientIp = null,
    int StatusCode = 200,
    string? RequestHeaders = null)
{
    public Timings? Timings => Usage.Timings;
}
