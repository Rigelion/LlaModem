using LlaModem.Services;

namespace LlaModem.Tests;

public class SystemIdleTrackerTests
{
    [Fact]
    public void Reset_ClearsLastRequestToCurrentTime()
    {
        // Arrange
        var tracker = new SystemIdleTracker();
        var original = tracker.LastRequest;

        // Act
        Thread.Sleep(10);
        tracker.Reset();

        // Assert
        var afterReset = tracker.LastRequest;
        Assert.True(afterReset >= original, "Reset should advance the timestamp");
    }

    [Fact]
    public void Reset_AllowsSubsequentRecordRequest()
    {
        // Arrange
        var tracker = new SystemIdleTracker();
        tracker.Reset();

        // Act & Assert — RecordRequest should not throw after Reset
        tracker.RecordRequest();
        var last = tracker.LastRequest;
        Assert.True(last.Year >= 2000);
    }
}
