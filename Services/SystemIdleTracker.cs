namespace LlaModem.Services;

public interface ISystemIdleTracker
{
    /// <summary>
    /// Records that a request has been received, updating the system-wide idle timestamp.
    /// </summary>
    void RecordRequest();

    /// <summary>
    /// The UTC time of the most recently recorded request. Used by idle timeout detection.
    /// </summary>
    DateTimeOffset LastRequest { get; }
}

public class SystemIdleTracker : ISystemIdleTracker
{
    private readonly object _lock = new();
    private DateTimeOffset _lastRequest = DateTimeOffset.UtcNow;

    public DateTimeOffset LastRequest
    {
        get
        {
            lock (_lock) { return _lastRequest; }
        }
    }

    public void RecordRequest()
    {
        lock (_lock) { _lastRequest = DateTimeOffset.UtcNow; }
    }
}
