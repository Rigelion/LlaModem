namespace LlaModem.Services;

/// <summary>
/// Represents the state of a model process.
/// </summary>
public record ModelProcessState(
    string ModelName,
    int ProcessId,
    DateTimeOffset StartedAt);

/// <summary>
/// Repository abstraction for tracking model process state.
/// Enables swapping in-memory tracking for other implementations (file, database, etc.).
/// </summary>
public interface IModelRepository
{
    /// <summary>
    /// Gets the current state for a model.
    /// </summary>
    Task<ModelProcessState?> GetStateAsync(string modelName, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the state for a model.
    /// </summary>
    Task SetStateAsync(ModelProcessState state, CancellationToken ct = default);

    /// <summary>
    /// Clears the state (model stopped).
    /// </summary>
    Task ClearStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all tracked processes.
    /// </summary>
    Task<IReadOnlyList<ModelProcessState>> GetAllStatesAsync(CancellationToken ct = default);
}
