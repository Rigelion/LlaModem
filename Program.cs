using LlaModem.Config;
using LlaModem.Middleware;
using LlaModem.Services;

namespace LlaModem;

public class Program
{
    public static void Main(string[] args)
    {
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
}
