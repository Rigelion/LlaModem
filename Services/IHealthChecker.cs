namespace LlaModem.Services;

/// <summary>
/// Checks whether an HTTP endpoint is healthy.
/// </summary>
public interface IHealthChecker
{
    /// <summary>
    /// Performs a single health check request. Returns (success, reason).
    /// </summary>
    Task<(bool success, string? reason)> CheckAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the URL repeatedly until it responds successfully or timeout elapses.
    /// </summary>
    Task<(bool success, string? reason)> PollAsync(string url, TimeSpan timeout, TimeSpan delay, CancellationToken cancellationToken = default);
}
