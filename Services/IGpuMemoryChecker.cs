namespace LlaModem.Services;

/// <summary>
/// Checks available VRAM and RAM before launching a model.
/// </summary>
public interface IGpuMemoryChecker
{
    /// <summary>
    /// Returns whether the system has enough free VRAM and RAM to safely start a model.
    /// </summary>
    /// <returns>A tuple of (isSufficient, errorMessage). If isSufficient is true, errorMessage is null.</returns>
    (bool isSufficient, string? errorMessage) CheckAvailableResources();
}
