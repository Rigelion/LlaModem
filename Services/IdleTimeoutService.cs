using Microsoft.Extensions.Options;
using LlaModem.Config;
using LlaModem.Services;

namespace LlaModem.Services;

public class IdleTimeoutService : BackgroundService
{
    private const int MinCheckIntervalSec = 15;
    private const int MaxCheckIntervalSec = 60;
    private const int CheckIntervalDivisor = 20;

    private readonly ModelManager _modelManager;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly RouterConfig _config;
    private readonly ILogger<IdleTimeoutService> _logger;
    private readonly PeriodicTimer _timer;

    /// <summary>
    /// Derives the idle-check polling interval from the configured timeout.
    /// Scales proportionally (timeout / divisor) with a floor and ceiling,
    /// so the check is never too aggressive on short timeouts or too lazy on long ones.
    /// </summary>
    private int CheckInterval =>
        Math.Min(MaxCheckIntervalSec, Math.Max(MinCheckIntervalSec, _config.Timeouts.IdleTimeoutSeconds / CheckIntervalDivisor));

    public IdleTimeoutService(
        ModelManager modelManager,
        SystemIdleTracker systemIdleTracker,
        IOptions<RouterConfig> config,
        ILogger<IdleTimeoutService> logger)
    {
        _modelManager = modelManager;
        _systemIdleTracker = systemIdleTracker;
        _config = config.Value;
        _logger = logger;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(CheckInterval));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Idle timeout service started (timeout: {Seconds}s)",
            _config.Timeouts.IdleTimeoutSeconds);

        try
        {
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

                    await _modelManager.StopActiveModelAsync();
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

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}
