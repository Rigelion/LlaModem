using System.Collections.Concurrent;

namespace LlaModem.Services;

/// <summary>
/// In-memory implementation of IModelRepository using ConcurrentDictionary.
/// Thread-safe and suitable for single-process scenarios.
/// </summary>
public sealed class InMemoryModelRepository : IModelRepository
{
    private readonly ConcurrentDictionary<string, ModelProcessState> _states = new();

    public async Task<ModelProcessState?> GetStateAsync(string modelName, CancellationToken ct = default)
    {
        await Task.Yield();
        return _states.TryGetValue(modelName, out var state) ? state : null;
    }

    public async Task SetStateAsync(ModelProcessState state, CancellationToken ct = default)
    {
        await Task.Yield();
        _states.AddOrUpdate(state.ModelName, state, (_, _) => state);
    }

    public async Task ClearStateAsync(CancellationToken ct = default)
    {
        await Task.Yield();
        _states.Clear();
    }

    public async Task<IReadOnlyList<ModelProcessState>> GetAllStatesAsync(CancellationToken ct = default)
    {
        await Task.Yield();
        return _states.Values.ToList();
    }
}
