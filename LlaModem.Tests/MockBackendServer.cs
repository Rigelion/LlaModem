using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace LlaModem.Tests;

/// <summary>
/// A minimal Kestrel-based HTTP server that mimics a llama-server backend.
/// </summary>
public class MockBackendServer : IAsyncDisposable
{
    private IHost? _host;
    private readonly int _port;

    public int Port => _port;
    public string Url => $"http://localhost:{_port}";

    public int RequestCount { get; private set; }
    public string? LastRequestBody { get; private set; }
    public int ResponseDelayMs { get; set; } = 0;
    public int? FailingStatusCode { get; set; }

    public MockBackendServer(int port) => _port = port;

    public async Task StartAsync()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(server => server.Listen(System.Net.IPAddress.Loopback, _port));
                webBuilder.Configure(app =>
                {
                    app.Run(async context =>
                    {
                        if (ResponseDelayMs > 0)
                            await Task.Delay(ResponseDelayMs, context.RequestAborted);

                        RequestCount++;

                        if (context.Request.ContentLength > 0)
                        {
                            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                            LastRequestBody = await reader.ReadToEndAsync();
                            context.Request.Body.Position = 0;
                        }

                        if (FailingStatusCode.HasValue)
                        {
                            context.Response.StatusCode = FailingStatusCode.Value;
                            return;
                        }

                        string responseBody;
                        var path = context.Request.Path.Value ?? "/";

                        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
                        {
                            responseBody = """{"status":"ok"}""";
                        }
                        else if (path.Contains("chat/completions"))
                        {
                            responseBody = """{"id":"chatcmpl-123","object":"chat.completion","created":0,"model":"qwen-mock","choices":[{"index":0,"message":{"role":"assistant","content":"Hello from mock!"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""";
                        }
                        else if (path.Contains("models"))
                        {
                            responseBody = """{"object":"list","data":[{"id":"qwen-smart","object":"model","created":0,"owned_by":"owner"}]}""";
                        }
                        else
                        {
                            responseBody = """{"status":"ok"}""";
                        }

                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(responseBody);
                    });
                });
            })
            .Build();

        await _host.StartAsync();
        await Task.Delay(100);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            await ((IAsyncDisposable)_host).DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
