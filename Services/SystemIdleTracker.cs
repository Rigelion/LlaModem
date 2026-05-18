namespace LlaModem.Services;

public class SystemIdleTracker
{
    private long _lastRequestTicks;

    public DateTimeOffset LastRequest => DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _lastRequestTicks));

    public void RecordRequest() => Interlocked.Exchange(ref _lastRequestTicks, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
