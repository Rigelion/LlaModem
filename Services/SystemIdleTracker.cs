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
    private long _lastRequestTicks;

    public DateTimeOffset LastRequest => DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _lastRequestTicks));

    public void RecordRequest() => Interlocked.Exchange(ref _lastRequestTicks, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
