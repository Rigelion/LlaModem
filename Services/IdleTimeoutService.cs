using Microsoft.Extensions.Options;
using LlaModem.Config;
using LlaModem.Services;

namespace LlaModem.Services;

public interface IIdleTimeoutResetter
{
    void StartIdleTracking();
    void StopIdleTracking();
}

public class IdleTimeoutService : BackgroundService, IIdleTimeoutResetter
{
    private const int MinCheckIntervalSec = 15;
    private const int MaxCheckIntervalSec = 60;
    private const int CheckIntervalDivisor = 20;

    private Func<CancellationToken, Task>? _stopActiveModel;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly RouterConfig _config;
    private readonly ILogger<IdleTimeoutService> _logger;
    private PeriodicTimer _timer;
    private ManualResetEventSlim _resetSignal = new ManualResetEventSlim(true);
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Derives the idle-check polling interval from the configured timeout.
    /// Scales proportionally (timeout / divisor) with a floor and ceiling,
    /// so the check is never too aggressive on short timeouts or too lazy on long ones.
    /// </summary>
    private int CheckInterval =>
        Math.Min(MaxCheckIntervalSec, Math.Max(MinCheckIntervalSec, _config.Timeouts.IdleTimeoutSeconds / CheckIntervalDivisor));

    public IdleTimeoutService(
        SystemIdleTracker systemIdleTracker,
        IOptions<RouterConfig> config,
        ILogger<IdleTimeoutService> logger)
    {
        _systemIdleTracker = systemIdleTracker;
        _config = config.Value;
        _logger = logger;
        // Callback is wired post-construction by ModelManager to avoid circular DI dependency
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(CheckInterval));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Idle timeout service started (timeout: {Seconds}s)",
            _config.Timeouts.IdleTimeoutSeconds);

        try
        {
            while (!_resetSignal.IsSet)
            {
                await Task.Delay(100, stoppingToken);
            }

            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                var elapsed = DateTimeOffset.UtcNow - _systemIdleTracker.LastRequest;
                if (elapsed.TotalSeconds >= _config.Timeouts.IdleTimeoutSeconds)
                {
                    _logger.LogInformation(
                        "Idle timeout reached ({Elapsed}s). Stopping active model...",
                        elapsed.TotalSeconds);

                    if (_stopActiveModel != null)
                    {
                        await _stopActiveModel(stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            _logger.LogInformation("Idle timeout service stopped");
        }
    }

    /// <summary>
    /// Sets the callback to invoke when idle timeout is reached.
    /// Wired by ModelManager after DI container is fully built.
    /// </summary>
    public void SetIdleStopCallback(Func<CancellationToken, Task> callback)
    {
        _stopActiveModel = callback;
    }

    public void StartIdleTracking()
    {
        _logger.LogInformation("Idle timeout tracking started (timeout: {Seconds}s)", _config.Timeouts.IdleTimeoutSeconds);
        _resetSignal = new ManualResetEventSlim(true);
        _stoppingCts?.Cancel();
        _stoppingCts = new CancellationTokenSource();
        _ = ExecuteAsync(_stoppingCts.Token);
    }

    public void StopIdleTracking()
    {
        _logger.LogInformation("Idle timeout tracking stopped");
        _stoppingCts?.Cancel();
        Dispose();
    }

    public override void Dispose()
    {
        _timer.Dispose();
        _resetSignal.Dispose();
        base.Dispose();
    }
}
