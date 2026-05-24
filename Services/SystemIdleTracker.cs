namespace LlaModem.Services;

public class SystemIdleTracker
{
    private long _lastRequestTicks;

    public SystemIdleTracker()
    {
        // Start the idle clock at app startup — no model is running yet, so
        // the first tick sees elapsed ≈ 0 instead of "56 years".
        _lastRequestTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public DateTimeOffset LastRequest => DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _lastRequestTicks));

    public void RecordRequest() => Interlocked.Exchange(ref _lastRequestTicks, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public void Reset() => Interlocked.Exchange(ref _lastRequestTicks, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
