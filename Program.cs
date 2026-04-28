using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;

namespace LlaModem;

public class Program
{
    public static void Main(string[] args)
    {
        LogProjectEnvironment();

        var builder = WebApplication.CreateBuilder(args);

        // Bind configuration
        builder.Services.Configure<AppConfig>(builder.Configuration);
        builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();

        builder.Services.Configure<RouterConfig>(builder.Configuration.GetSection("Router"));
        builder.Services.AddOptions<RouterConfig>().Bind(builder.Configuration.GetSection("Router")).ValidateOnStart();

        // Configure Kestrel to listen on the configured URL
        var routerConfig = builder.Configuration.GetSection("Router");
        var listenUrl = routerConfig["ListenUrl"] ?? "http://localhost:9000";
        var uri = new Uri(listenUrl);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.ListenAnyIP(uri.Port);
        });

        // Register services
        builder.Services.AddSingleton<IModelLauncher, DefaultModelLauncher>();
        builder.Services.AddSingleton<ModelManager>();
        builder.Services.AddSingleton<IRequestTracker, RequestTracker>();
        builder.Services.AddHostedService<IdleTimeoutService>();

        var app = builder.Build();

        // Apply Basic Auth to /v1/* routes
        app.UseBasicAuthWhen("/v1");

        app.ConfigureEndpoints();

        app.Run();
    }

    private static void LogProjectEnvironment()
    {
        var projectVars = new[]
        {
            "QWEN_SMART_START_SCRIPT",
            "QWEN_FAST_START_SCRIPT",
            "LLAMODEM_LISTEN_URL",
            "LLAMODEM_AUTH_USERNAME",
            "LLAMODEM_AUTH_PASSWORD",
            "ASPNETCORE_ENVIRONMENT"
        };

        Console.WriteLine("=== LlaModem Environment ===");
        foreach (var key in projectVars)
        {
            var value = Environment.GetEnvironmentVariable(key);
            var displayValue = string.IsNullOrEmpty(value)
                ? "(not set)"
                : value.Contains(" ") || value.Contains("=") ? $"\"{value}\"" : value;
            Console.WriteLine($"  {key}={displayValue}");
        }
        Console.WriteLine();
    }
}
