using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// Service for persisting token usage statistics to daily markdown files.
/// </summary>
public interface IUsageService
{
    /// <summary>Record a single request's token usage.</summary>
    void Record(SessionEntry entry);
}
