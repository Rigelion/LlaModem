using LlaModem.Services;

namespace LlaModem.Tests;

public class InMemoryModelRepositoryTests
{
    [Fact]
    public async Task GetStateAsync_InitialState_ReturnsNull()
    {
        var repo = new InMemoryModelRepository();
        var state = await repo.GetStateAsync("qwen-smart");
        Assert.Null(state);
    }

    [Fact]
    public async Task SetStateAsync_UpdatesModelState()
    {
        var repo = new InMemoryModelRepository();
        var state = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state);

        var retrieved = await repo.GetStateAsync("qwen-smart");
        Assert.NotNull(retrieved);
        Assert.Equal("qwen-smart", retrieved!.ModelName);
        Assert.Equal(12345, retrieved.ProcessId);
    }

    [Fact]
    public async Task SetStateAsync_AlsoSetsActiveKey()
    {
        var repo = new InMemoryModelRepository();
        var state = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state);

        var activeState = await repo.GetStateAsync("active");
        Assert.NotNull(activeState);
        Assert.Equal("qwen-smart", activeState!.ModelName);
        Assert.Equal(12345, activeState.ProcessId);
    }

    [Fact]
    public async Task GetActiveModelNameAsync_ReturnsCorrectModel()
    {
        var repo = new InMemoryModelRepository();
        var state = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state);

        var activeModelName = await repo.GetStateAsync("active");
        Assert.NotNull(activeModelName);
        Assert.Equal("qwen-smart", activeModelName!.ModelName);
    }

    [Fact]
    public async Task SetStateAsync_UpdatesActiveKeyWhenSwitchingModels()
    {
        var repo = new InMemoryModelRepository();
        var state1 = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state1);

        var state2 = new ModelProcessState("qwen-fast", 67890, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state2);

        var activeState = await repo.GetStateAsync("active");
        Assert.NotNull(activeState);
        Assert.Equal("qwen-fast", activeState!.ModelName);
        Assert.Equal(67890, activeState.ProcessId);

        var fastState = await repo.GetStateAsync("qwen-fast");
        Assert.NotNull(fastState);
        Assert.Equal(67890, fastState!.ProcessId);
    }

    [Fact]
    public async Task ClearStateAsync_RemovesAllStatesIncludingActive()
    {
        var repo = new InMemoryModelRepository();
        var state = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state);

        await repo.ClearStateAsync();

        var retrieved = await repo.GetStateAsync("qwen-smart");
        Assert.Null(retrieved);

        var activeState = await repo.GetStateAsync("active");
        Assert.Null(activeState);
    }

    [Fact]
    public async Task GetAllStatesAsync_ReturnsAllTrackedModels()
    {
        var repo = new InMemoryModelRepository();
        var state1 = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        var state2 = new ModelProcessState("qwen-fast", 67890, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state1);
        await repo.SetStateAsync(state2);

        var all = await repo.GetAllStatesAsync();
        Assert.Equal(3, all.Count); // qwen-smart, qwen-fast, active
        Assert.Contains(all, s => s.ModelName == "qwen-smart");
        Assert.Contains(all, s => s.ModelName == "qwen-fast");
        Assert.Contains(all, s => s.ModelName == "qwen-fast"); // active points to last set model
    }

    [Fact]
    public async Task SetStateAsync_OverwritesExistingState()
    {
        var repo = new InMemoryModelRepository();
        var state1 = new ModelProcessState("qwen-smart", 12345, DateTimeOffset.UtcNow);
        await repo.SetStateAsync(state1);

        var state2 = new ModelProcessState("qwen-smart", 54321, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1));
        await repo.SetStateAsync(state2);

        var retrieved = await repo.GetStateAsync("qwen-smart");
        Assert.NotNull(retrieved);
        Assert.Equal(54321, retrieved!.ProcessId);
    }
}
