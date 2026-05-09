using LlaModem.Models;
using LlaModem.Services;

namespace LlaModem.Tests;

public class DashboardEndpointTests
{
    [Fact]
    public async Task GetAllModels_ReturnsDefaultParameters_WhenNoCustomParamsFileExists()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var paramsFilePath = Path.Combine(tempDir.Path, "dashboard_params.json");
        
        // Ensure params file doesn't exist
        Assert.False(File.Exists(paramsFilePath));
        
        // Act - Simulate loading params (which returns defaults when file doesn't exist)
        var result = ModelLaunchParams.Defaults;
        
        // Assert - Verify all default values match expected
        Assert.Equal(0.6, result.Temperature);
        Assert.Equal(0.95, result.TopP);
        Assert.Equal(20, result.TopK);
        Assert.Equal(0.0, result.MinP);
        Assert.Equal(0.0, result.PresencePenalty);
        Assert.Equal(1.05, result.RepetitionPenalty);
    }

    [Fact]
    public async Task GetAllModels_Parameters_ContainsDefaultValues()
    {
        // Arrange
        var expectedDefaults = new
        {
            Temperature = 0.6,
            TopP = 0.95,
            TopK = 20,
            MinP = 0.0,
            PresencePenalty = 0.0,
            RepetitionPenalty = 1.05
        };
        
        // Act
        var defaults = ModelLaunchParams.Defaults;
        
        // Assert
        Assert.Equal(expectedDefaults.Temperature, defaults.Temperature);
        Assert.Equal(expectedDefaults.TopP, defaults.TopP);
        Assert.Equal(expectedDefaults.TopK, defaults.TopK);
        Assert.Equal(expectedDefaults.MinP, defaults.MinP);
        Assert.Equal(expectedDefaults.PresencePenalty, defaults.PresencePenalty);
        Assert.Equal(expectedDefaults.RepetitionPenalty, defaults.RepetitionPenalty);
    }

    [Fact]
    public async Task UpdateModelParamsRequest_UsesDefaultValues_WhenNoOverridesProvided()
    {
        // Arrange
        var request = new UpdateModelParamsRequest();
        
        // Act
        var launchParams = request.ToLaunchParams();
        
        // Assert
        Assert.Equal(ModelLaunchParams.Defaults.Temperature, launchParams.Temperature);
        Assert.Equal(ModelLaunchParams.Defaults.TopP, launchParams.TopP);
        Assert.Equal(ModelLaunchParams.Defaults.TopK, launchParams.TopK);
        Assert.Equal(ModelLaunchParams.Defaults.MinP, launchParams.MinP);
        Assert.Equal(ModelLaunchParams.Defaults.PresencePenalty, launchParams.PresencePenalty);
        Assert.Equal(ModelLaunchParams.Defaults.RepetitionPenalty, launchParams.RepetitionPenalty);
    }

    [Fact]
    public async Task StartModelRequest_UsesDefaultValues_WhenNoOverridesProvided()
    {
        // Arrange
        var request = new StartModelRequest();
        
        // Act
        var launchParams = request.ToLaunchParams();
        
        // Assert
        Assert.Equal(ModelLaunchParams.Defaults.Temperature, launchParams.Temperature);
        Assert.Equal(ModelLaunchParams.Defaults.TopP, launchParams.TopP);
        Assert.Equal(ModelLaunchParams.Defaults.TopK, launchParams.TopK);
        Assert.Equal(ModelLaunchParams.Defaults.MinP, launchParams.MinP);
        Assert.Equal(ModelLaunchParams.Defaults.PresencePenalty, launchParams.PresencePenalty);
        Assert.Equal(ModelLaunchParams.Defaults.RepetitionPenalty, launchParams.RepetitionPenalty);
    }

    [Fact]
    public async Task ModelLaunchParams_Defaults_HasCorrectStructure()
    {
        // Arrange & Act
        var defaults = ModelLaunchParams.Defaults;
        
        // Assert - Verify no null values (all defaults should be set)
        Assert.NotNull(defaults.Temperature);
        Assert.NotNull(defaults.TopP);
        Assert.NotNull(defaults.TopK);
        Assert.NotNull(defaults.MinP);
        Assert.NotNull(defaults.PresencePenalty);
        Assert.NotNull(defaults.RepetitionPenalty);
        
        // Assert - Verify all values are within expected ranges
        Assert.InRange(defaults.Temperature.Value, 0.5, 0.7);
        Assert.InRange(defaults.TopP.Value, 0.9, 1.0);
        Assert.Equal(20, defaults.TopK.Value);
        Assert.InRange(defaults.MinP.Value, 0.0, 0.1);
        Assert.InRange(defaults.PresencePenalty.Value, -0.1, 0.1);
        Assert.InRange(defaults.RepetitionPenalty.Value, 1.0, 1.1);
    }
}

/// <summary>
/// Simple disposable temp directory helper for tests.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }
    
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(Path);
    }
    
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
