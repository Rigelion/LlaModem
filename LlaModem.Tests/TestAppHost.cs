using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace LlaModem.Tests;

/// <summary>
/// Integration test host: starts mock backends + the LlaModem app as a real process.
/// </summary>
public class TestAppHost : IAsyncLifetime
{
    private readonly MockBackendServer _smartServer;
    private readonly MockBackendServer _fastServer;
    private Process? _appProcess;
    private readonly int _routerPort;
    private string? _noOpScript;

    public HttpClient Client { get; private set; } = null!;

    public string RouterUrl => $"http://localhost:{_routerPort}";
    public MockBackendServer SmartServer => _smartServer;
    public MockBackendServer FastServer => _fastServer;

    public TestAppHost()
    {
        _routerPort = GetRandomUnusedPort();
        _smartServer = new MockBackendServer(0);
        _fastServer = new MockBackendServer(0);
    }

    public async Task InitializeAsync()
    {
        // Start mock backends first
        await _smartServer.StartAsync();
        await _fastServer.StartAsync();

        _noOpScript = Path.Combine(Path.GetTempPath(), $"noop-start-{Guid.NewGuid()}.ps1");
        File.WriteAllText(_noOpScript, "@exit 0");

        // Create appsettings.test.json
        var configPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location!)!, "appsettings.test.json");
        var config = new
        {
            Router = new
            {
                ListenUrl = $"http://localhost:{_routerPort}",
                AuthUsername = "testuser",
                AuthPassword = "testpass",
                IdleTimeoutSeconds = 600
            },
            Models = new Dictionary<string, object>
            {
                ["qwen-smart"] = new { StartScript = _noOpScript, BackendUrl = _smartServer.Url },
                ["qwen-fast"] = new { StartScript = _noOpScript, BackendUrl = _fastServer.Url }
            }
        };
        File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        // Start the app
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project .. --no-build --framework net10.0 --environment test --contentRoot .",
            WorkingDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location!)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "test",
                ["ASPNETCORE_URLS"] = $"http://localhost:{_routerPort}"
            }
        };

        _appProcess = Process.Start(processStartInfo);
        if (_appProcess == null)
            throw new InvalidOperationException("Failed to start LlaModem process");

        // Wait for server to be ready
        var retries = 30;
        while (retries-- > 0)
        {
            await Task.Delay(500);
            try
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                var resp = await hc.GetAsync($"http://localhost:{_routerPort}/health");
                if (resp.IsSuccessStatusCode) break;
            }
            catch { /* not ready yet */ }
        }

        Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _appProcess?.Kill(true);
        _appProcess?.WaitForExit(5000);
        _appProcess?.Dispose();
        await _smartServer.StopAsync();
        await _fastServer.StopAsync();
        if (_noOpScript != null && File.Exists(_noOpScript))
            File.Delete(_noOpScript);
        var configPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location!)!, "appsettings.test.json");
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    private static int GetRandomUnusedPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
