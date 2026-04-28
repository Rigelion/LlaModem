using Microsoft.Extensions.Options;
using LlaModem.Config;
using LlaModem.Services;

namespace LlaModem.Services;

public class IdleTimeoutService : BackgroundService
{
    private readonly ModelManager _modelManager;
    private readonly IRequestTracker _requestTracker;
    private readonly RouterConfig _config;
    private readonly ILogger<IdleTimeoutService> _logger;
    private readonly PeriodicTimer _timer;

    public IdleTimeoutService(
        ModelManager modelManager,
        IRequestTracker requestTracker,
        IOptions<RouterConfig> config,
        ILogger<IdleTimeoutService> logger)
    {
        _modelManager = modelManager;
        _requestTracker = requestTracker;
        _config = config.Value;
        _logger = logger;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Idle timeout service started (timeout: {Seconds}s)",
            _config.IdleTimeoutSeconds);

        try
        {
            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                var elapsed = DateTimeOffset.UtcNow - _requestTracker.LastRequest;
                if (elapsed.TotalSeconds >= _config.IdleTimeoutSeconds)
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
