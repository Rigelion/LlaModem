using System.Diagnostics;
using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

public class HealthChecker : IHealthChecker
{
    private readonly RouterConfig.TimeoutConfig _timeouts;
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthChecker(IOptions<RouterConfig> config, IHttpClientFactory httpClientFactory)
    {
        _timeouts = config.Value.Timeouts;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> CheckAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(_timeouts.HealthCheckTimeoutSeconds);
            var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PollAsync(string url, TimeSpan timeout, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Reuse a single HttpClient across the polling loop to avoid creating
        // a new client (and underlying socket) on every poll attempt.
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(_timeouts.HealthCheckPollTimeoutMinutes);

        while (sw.Elapsed < timeout)
        {
            try
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Backend not ready yet — retry
            }

            await Task.Delay(delay, cancellationToken);
        }

        return false;
    }
}
