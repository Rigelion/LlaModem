namespace LlaModem.Services;

/// <summary>
/// Interface for model lifecycle management operations.
/// Enables swapping ModelManager implementation for testing or alternative orchestrators.
/// </summary>
public interface IMetaModelManager
{
    /// <summary>
    /// Gets the current active model name by querying the repository.
    /// </summary>
    Task<string?> GetActiveModelNameAsync(CancellationToken ct = default);

    /// <summary>
    /// True if no model is currently active.
    /// </summary>
    Task<bool> IsIdleAsync(CancellationToken ct = default);

    /// <summary>
    /// Ensures the requested model is running. Starts it if needed, switches if different model is active.
    /// </summary>
    Task EnsureModelAsync(string modelName, ModelLaunchParams? launchParams = null, CancellationToken ct = default);

    /// <summary>
    /// Gracefully stops the currently active model.
    /// </summary>
    Task StopActiveModelAsync(CancellationToken ct = default);
}
