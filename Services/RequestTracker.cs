namespace LlaModem.Services;

public interface IRequestTracker
{
    void RecordRequest();
    DateTimeOffset LastRequest { get; }
}

public class RequestTracker : IRequestTracker
{
    private object _lock = new();
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
