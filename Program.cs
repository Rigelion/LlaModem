using LlamaDem.Config;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration
builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.Run();
