using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace LlaModem;

public class Program
{
    public static void Main(string[] args)
    {
        LogProjectEnvironment();

        var builder = WebApplication.CreateBuilder(args);
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .CreateLogger();
        builder.Host.UseSerilog(logger);

        // Bind configuration (AddOptions + Bind replaces Configure — no duplication)
        builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();
        builder.Services.AddOptions<RouterConfig>().Bind(builder.Configuration.GetSection("Router")).ValidateOnStart();
        builder.Services.AddOptions<UsageConfig>().Bind(builder.Configuration.GetSection(UsageConfig.SectionName)).ValidateOnStart();

        // Expand environment variables in ModelConfig.StartScript at binding time (pure function)
        builder.Services.Configure<AppConfig>(config =>
        {
            foreach (var model in config.Models.Values)
            {
                model.StartScript = Environment.ExpandEnvironmentVariables(model.StartScript);
            }
        });
        builder.Services.AddSingleton<IUsageService, UsageService>();
        builder.Services.AddSingleton<IStatsService, StatsService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<UsageConfig>>().Value;
            var basePath = config.Path;
            var fullPath = Path.IsPathRooted(basePath)
                ? basePath
                : Path.Combine(AppContext.BaseDirectory, basePath);
            return new StatsService($"Data Source={fullPath}");
        });

        // Configure Kestrel to listen on the configured URL
        var routerConfig = builder.Configuration.GetSection("Router");
        var listenUrl = routerConfig["ListenUrl"] ?? "http://localhost:9000";
        var uri = new Uri(listenUrl);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.ListenAnyIP(uri.Port);
        });

        // Register services (concrete types only — no unnecessary interfaces)
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient("ModelManager", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        builder.Services.AddSingleton<HealthChecker>();
        builder.Services.AddSingleton<ProcessKiller>();
        builder.Services.AddSingleton<IModelRepository, InMemoryModelRepository>();
        builder.Services.AddSingleton<DefaultModelLauncher>();
        builder.Services.AddSingleton<GpuMemoryChecker>();
        builder.Services.AddSingleton<ModelManager>();
        builder.Services.AddSingleton<ModelMetricsService>();
        builder.Services.AddSingleton<DashboardService>();
        builder.Services.AddSingleton<IUsagePersistence, SqliteUsagePersistence>();
        builder.Services.AddSingleton<IStatsService, StatsService>();
        builder.Services.AddSingleton<UsageService>();
        builder.Services.AddSingleton<SystemIdleTracker>();
        builder.Services.AddSingleton<LaunchParamParser>();
        builder.Services.AddSingleton<RequestForwarder>();
        builder.Services.AddSingleton<ModelProxyHandler>();
        builder.Services.AddHostedService<IdleTimeoutService>();

        // Register header value injector with configured mappings
        builder.Services.AddSingleton<HeaderValueInjector>(sp =>
        {
            var routerConfig = sp.GetRequiredService<IOptions<RouterConfig>>().Value;
            return new HeaderValueInjector(routerConfig.EnableBodyHeaderInjection, routerConfig.BodyHeaderMappings);
        });

        var app = builder.Build();

        // Register shutdown handler to kill all tracked PowerShell windows
        var modelManager = app.Services.GetRequiredService<ModelManager>();
        var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        appLifetime.ApplicationStopping.Register(async () =>
        {
            appLogger.LogInformation("Application shutdown initiated — shutting down all tracked processes");
            await modelManager.ShutdownAsync(appLifetime.ApplicationStopping);
        });

        // Response logging + usage capture middleware (combined — single buffer pass)
        var usageConfig = app.Configuration.GetSection(UsageConfig.SectionName).Get<UsageConfig>();
        if (usageConfig?.Enabled == true)
            app.UseResponseUsageCapture();

        // Request logging middleware (after response capture, so response size is available)
        app.UseRequestLogging();

        // Apply Basic Auth to /v1/* routes
        app.UseBasicAuthWhen("/v1");

        app.ConfigureEndpoints();
        app.MapStatsEndpoints();

        app.Run();
    }

    private static void LogProjectEnvironment()
    {
        var sensitiveVars = new[] { "LLAMODEM_AUTH_PASSWORD" };

        var projectVars = new[]
        {
            "QWEN_SMART_START_SCRIPT",
            "QWEN_FAST_START_SCRIPT",
            "LLAMODEM_BACKEND_URL",
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
                : sensitiveVars.Contains(key)
                    ? "***masked***"
                    : value.Contains(" ") || value.Contains("=") ? $"\"{value}\"" : value;
            Console.WriteLine($"  {key}={displayValue}");
        }
        Console.WriteLine();
    }
}
