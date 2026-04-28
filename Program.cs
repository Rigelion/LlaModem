using LlamaDem.Config;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration
builder.Services.Configure<AppConfig>(builder.Configuration);
builder.Services.AddOptions<AppConfig>().Bind(builder.Configuration).ValidateOnStart();

var app = builder.Build();

// Health endpoint (unauthenticated)
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

// Apply Basic Auth to /v1/* routes
app.UseBasicAuthWhen("/v1");

// v1 routes (placeholder — full implementation in Phase 4)
app.Map("/v1/", () => "LlamaDem Router — OpenAI-compatible API");

app.Run();
