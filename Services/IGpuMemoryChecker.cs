namespace LlaModem.Services;

/// <summary>
/// Checks available VRAM before launching a model on Windows.
/// </summary>
public interface IGpuMemoryChecker
{
    /// <summary>
    /// Returns whether the system has enough free VRAM to safely start a model.
    /// </summary>
    /// <returns>A tuple of (isSufficient, errorMessage). If isSufficient is true, errorMessage is null.</returns>
    (bool isSufficient, string? errorMessage) CheckAvailableResources();
}
