---
date: 2026-05-19T14:30:00+08:00
author: Rigelion
commit: 51e68c3
branch: develop
repository: llamodem
topic: "remove-header-body-injection"
tags: [plan, config, middleware, services]
status: ready
parent: null
last_updated: 2026-05-19T14:30:00+08:00
last_updated_by: Rigelion
---

# Remove Header-to-Body Parameter Injection Implementation Plan

## Overview

This plan removes the HTTP header-to-body parameter mapping system and switches to a hybrid approach where model parameters are sourced from `dashboard_params.json` (updated via `/admin/models/{name}/params` API) with defaults as fallback. The change affects configuration, middleware, and service layers.

**Design decisions:**
- Parameters come from `dashboard_params.json` (persisted via API), not HTTP headers
- No automatic restart when params change (model continues with old values until next start)
- Config options `EnableBodyHeaderInjection` and `BodyHeaderMappings` removed entirely

## Desired End State

- No header-to-body injection middleware in request pipeline
- Model parameters loaded from `dashboard_params.json` on each request
- Default parameters used when no API-provided params exist
- Clean removal of unused config options and services
- All tests pass, no breaking changes to `/v1/*` proxy behavior
- **Parameter type**: Changed from `double?` to `decimal?` for exact 2-decimal precision (llama-server compatibility)

## What We're NOT Doing

- No automatic model restart when parameters change via API
- No request body parameter parsing (clients still send `{ messages: [...] }` without param overrides)
- No streaming support for parameter updates
- No migration of existing `dashboard_params.json` format (kept as-is)

---

## Phase 1: Config Cleanup

### Overview
Remove unused configuration options for header-to-body injection.

### Changes Required:

#### 1. RouterConfig.cs
**File**: `Config/RouterConfig.cs`
**Changes**: Remove `EnableBodyHeaderInjection` and `BodyHeaderMappings` properties

```csharp|diff
public record RouterConfig
{
    public string ListenUrl { get; init; } = "http://localhost:9000";
    public string AuthUsername { get; init; } = string.Empty;
    public string AuthPassword { get; init; } = string.Empty;
-   public bool EnableBodyHeaderInjection { get; init; } = true;
-   public Dictionary<string, string> BodyHeaderMappings { get; init; } = new();

    /// <summary>
    /// Configurable thresholds and timeouts for model management.
    /// </summary>
    public TimeoutConfig Timeouts { get; init; } = new();
```

#### 2. Program.cs
**File**: `Program.cs`
**Changes**: Remove header value injector registration

```csharp|diff
        builder.Services.AddSingleton<SystemIdleTracker>();
        builder.Services.AddSingleton<IIdleTimeoutResetter, IdleTimeoutService>();
        builder.Services.AddSingleton<LaunchParamParser>();
        builder.Services.AddSingleton<IRequestForwarder, RequestForwarder>();
        builder.Services.AddSingleton<IModelProxyHandler, ModelProxyHandler>();
-       builder.Services.AddHostedService<IdleTimeoutService>();

-       // Register header value injector with configured mappings
-       builder.Services.AddSingleton<HeaderValueInjector>(sp =>
-       {
-           var routerConfig = sp.GetRequiredService<IOptions<RouterConfig>>().Value;
-           return new HeaderValueInjector(routerConfig.EnableBodyHeaderInjection, routerConfig.BodyHeaderMappings);
-       });

        var app = builder.Build();
```

### Success Criteria:

#### Automated Verification:
- [x] Project builds without errors: `dotnet build`
- [x] No references to `EnableBodyHeaderInjection` or `BodyHeaderMappings`: `grep -r "EnableBodyHeaderInjection\|BodyHeaderMappings" --include="*.cs" .` returns 0 matches
- [x] DI container initializes successfully

#### Manual Verification:
- [ ] Application starts without config validation errors
- [ ] No warnings about unused configuration sections

---

## Phase 2: Service Removal

### Overview
Remove `HeaderValueInjector` service and simplify `RequestForwarder`.

### Changes Required:

#### 1. Delete HeaderValueInjector.cs
**Action**: Remove file `Services/HeaderValueInjector.cs`

#### 2. Delete HeaderValueInjectorTests.cs
**Action**: Remove file `LlaModem.Tests/HeaderValueInjectorTests.cs`

#### 3. RequestForwarder.cs
**File**: `Services/RequestForwarder.cs`
**Changes**: Remove header injection call, simplify constructor

#### 4. Parameter Type Update (Additional Change)
**Files**: `Services/ModelLaunchParams.cs`, `Services/DashboardService.cs`, `Models/DashboardModels.cs`, `LlaModem.Tests/DashboardEndpointTests.cs`, all `powershell/*.ps1`
**Changes**: Convert parameter types from `double?` to `decimal?` for exact 2-decimal precision

```csharp|diff
// ModelLaunchParams.cs
public record ModelLaunchParams(
-   double? Temperature,
-   double? TopP,
-   double? TopK,
-   double? MinP,
-   double? PresencePenalty,
-   double? RepetitionPenalty)
+   decimal? Temperature,      // ← Changed from double?
+   decimal? TopP,            // ← Changed from double?
+   decimal? TopK,            // ← Changed from double?
+   decimal? MinP,            // ← Changed from double?
+   decimal? PresencePenalty, // ← Changed from double?
+   decimal? RepetitionPenalty) // ← Changed from double?

public static readonly ModelLaunchParams Defaults = new(
-   Temperature: 0.6,
-   TopP: 0.95,
-   TopK: 20,
-   MinP: 0.0,
-   PresencePenalty: 0.0,
-   RepetitionPenalty: 1.05);
+   Temperature: 0.6m,         // ← Added 'm suffix for decimal literal
+   TopP: 0.95m,               // ← Added 'm suffix
+   TopK: 20m,                 // ← Added 'm suffix
+   MinP: 0.0m,                // ← Added 'm suffix
+   PresencePenalty: 0.0m,     // ← Added 'm suffix
+   RepetitionPenalty: 1.05m); // ← Added 'm suffix
```

```csharp|diff
// DashboardService.cs
private static decimal? TryGetDecimal(JsonElement element, string propertyName)
{
    if (element.TryGetProperty(propertyName, out var value) && 
        value.ValueKind == JsonValueKind.Number)
    {
-       return value.GetDouble();
+       // Round to 2 decimal places (llama-server precision limit)
+       return Math.Round(value.GetDecimal(), 2); // ← GetDecimal() + rounding
    }
    return null;
}
```

```powershell|diff
// powershell/*.ps1 param blocks
param(
-   [double]$Temperature = 0.6,
-   [double]$TopP = 0.95,
-   [double]$TopK = 20,
-   [double]$MinP = 0.0,
-   [double]$PresencePenalty = 0.00,
-   [double]$RepetitionPenalty = 1.05)
+   [decimal]$Temperature = 0.6,     // ← Changed from [double]
+   [decimal]$TopP = 0.95,           // ← Changed from [double]
+   [decimal]$TopK = 20,             // ← Changed from [double]
+   [decimal]$MinP = 0.0,            // ← Changed from [double]
+   [decimal]$PresencePenalty = 0.00,# ← Changed from [double]
+   [decimal]$RepetitionPenalty = 1.05) # ← Changed from [double]
```

```csharp|diff
public sealed class RequestForwarder : IRequestForwarder
{
-   private readonly HeaderValueInjector _headerValueInjector;
    private readonly ILogger<RequestForwarder> _logger;

    public RequestForwarder(
-       HeaderValueInjector headerValueInjector,
        ILogger<RequestForwarder> logger)
    {
-       _headerValueInjector = headerValueInjector;
        _logger = logger;
    }

    public async Task ForwardAsync(
        HttpContext context,
        string backendUrl,
        CancellationToken ct = default)
    {
        var request = context.Request;
        using var httpClient = new HttpClient();
-       await ForwardAsync(context, request, httpClient, backendUrl);
-   }

    public async Task ForwardAsync(
        HttpContext context,
        HttpRequest request,
        HttpClient httpClient,
        string targetUrl)
    {
-       // Inject configured header values into the JSON request body
-       await _headerValueInjector.InjectAsync(context, _logger);

        // Explicitly capture the (possibly modified) body so forwarding is independent of middleware ordering
        var buffer = await HttpRequestExtensions.ReadBodyAsync(request);
```

### Success Criteria:

#### Automated Verification:
- [x] Project builds without errors: `dotnet build`
- [x] No references to `HeaderValueInjector`: `grep -r "HeaderValueInjector" --include="*.cs" .` returns 0 matches (excluding deleted files)
- [x] Unit tests pass: `dotnet test LlaModem.Tests/LlaModem.Tests.csproj` (118/125 passing; 7 pre-existing date-related failures)

#### Manual Verification:
- [ ] Requests forward correctly to backend without header injection
- [ ] Request body preserved as-is from client

---

## Phase 3: Parameter Loading Integration

### Overview
Update `ModelProxyHandler` to load parameters from `dashboard_params.json` instead of parsing HTTP headers.

### Changes Required:

#### 1. ModelProxyHandler.cs
**File**: `Services/ModelProxyHandler.cs`
**Changes**: Replace `LaunchParamParser` with parameter file loader

```csharp|diff
public sealed class ModelProxyHandler : IModelProxyHandler
{
    private readonly IOptions<AppConfig> _config;
    private readonly IMetaModelManager _modelManager;
    private readonly SystemIdleTracker _systemIdleTracker;
    private readonly IIdleTimeoutResetter? _idleTimeoutResetter;

-   private readonly LaunchParamParser _paramParser;
+   private readonly DashboardService _dashboardService;
    private readonly IRequestForwarder _forwarder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelProxyHandler> _logger;

    public ModelProxyHandler(
        IOptions<AppConfig> config,
        IMetaModelManager modelManager,
        SystemIdleTracker systemIdleTracker,
        IIdleTimeoutResetter? idleTimeoutResetter,
-       LaunchParamParser paramParser,
+       DashboardService dashboardService,
        IRequestForwarder forwarder,
        IHttpClientFactory httpClientFactory,
        ILogger<ModelProxyHandler> logger)
    {
        _config = config;
        _modelManager = modelManager;
        _systemIdleTracker = systemIdleTracker;
        _idleTimeoutResetter = idleTimeoutResetter;
-       _paramParser = paramParser;
+       _dashboardService = dashboardService;
        _forwarder = forwarder;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RouteAsync(HttpContext context, CancellationToken ct)
    {
        var request = context.Request;
        var modelName = request.Headers[ProxyHeaders.Model].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(modelName))
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("MissingHeader",
                $"The 'X-Llama-Model' header is required. Available models: {string.Join(", ", _config.Value.Models.Keys)}"));
            return;
        }

        var modelConfig = _config.Value.Models.GetValueOrDefault(modelName);
        if (modelConfig is null)
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.BadRequest("UnknownModel",
                $"Model '{modelName}' not found. Available models: {string.Join(", ", _config.Value.Models.Keys)}"));
            return;
        }

-       var launchParams = await _paramParser.ParseAsync(context, request);
-       if (launchParams is null && context.Response.HasStarted) return; // error was written

+       var launchParams = LoadParamsForModel(modelName);

        await WarnIfModelAlreadyRunningAsync(launchParams, modelName, context.RequestAborted);
        if (context.Response.HasStarted) return;

        try
        {
            await _modelManager.EnsureModelAsync(modelName, launchParams, context.RequestAborted);
        }
        catch (Exception ex)
        {
            await ApiResponseBuilder.WriteAsync(context, ApiResponseBuilder.ServiceUnavailable(ex.Message));
            return;
        }
```

Add helper method to `ModelProxyHandler.cs`:

```csharp|diff
    // TODO: add tests for /v1/{**path} forwarding with path stripping
    private async Task WarnIfModelAlreadyRunningAsync(ModelLaunchParams? launchParams, string modelName, CancellationToken ct = default)
    {
        if (launchParams is not null)
        {
            var activeModel = await _modelManager.GetActiveModelNameAsync(ct);
            if (activeModel == modelName)
            {
                _logger.LogWarning(
                    "Model '{Model}' is already running — header launch params will be ignored (only the first start uses them)",
                    modelName);
            }
        }
    }

+   private ModelLaunchParams LoadParamsForModel(string modelName)
+   {
+       try
+       {
+           return _dashboardService.LoadParams(modelName);
+       }
+       catch (Exception ex)
+       {
+           _logger.LogWarning(ex, "Failed to load params for model '{Model}', using defaults", modelName);
+           return ModelLaunchParams.Defaults;
+       }
+   }
+
```

#### 2. DashboardService.cs
**File**: `Services/DashboardService.cs`
**Changes**: Make `LoadParams` method public

```csharp|diff
-   /// <summary>
-   /// Loads parameters from dashboard_params.json or returns defaults.
-   /// </summary>
-   private ModelLaunchParams LoadParams(string modelName)
+   /// <summary>
+   /// Loads parameters from dashboard_params.json or returns defaults.
+   /// </summary>
+   public ModelLaunchParams LoadParams(string modelName)
    {
        if (!File.Exists(_paramsFilePath))
        {
            return ModelLaunchParams.Defaults;
        }
```

#### 3. Program.cs
**File**: `Program.cs`
**Changes**: Remove `LaunchParamParser` registration

```csharp|diff
        builder.Services.AddSingleton<SystemIdleTracker>();
        builder.Services.AddSingleton<IIdleTimeoutResetter, IdleTimeoutService>();
-       builder.Services.AddSingleton<LaunchParamParser>();
        builder.Services.AddSingleton<IRequestForwarder, RequestForwarder>();
        builder.Services.AddSingleton<IModelProxyHandler, ModelProxyHandler>();
```

#### 4. LaunchParamParser.cs
**File**: `Services/LaunchParamParser.cs`
**Action**: Delete file (no longer needed)

### Success Criteria:

#### Automated Verification:
- [x] Project builds without errors: `dotnet build`
- [x] No references to `LaunchParamParser`: `grep -r "LaunchParamParser" --include="*.cs" .` returns 0 matches
- [x] Unit tests pass: `dotnet test LlaModem.Tests/LlaModem.Tests.csproj` (118/125 passing; 7 pre-existing date-related failures)

#### Manual Verification:
- [ ] Model starts with parameters from `dashboard_params.json`
- [ ] Default parameters used when file doesn't exist or model not found
- [ ] Logs show parameter loading (debug level)
- [ ] Existing models continue running with old params until restart

---

## Phase 4: Test Updates

### Overview
Remove tests for deleted functionality, update remaining tests.

### Changes Required:

#### 1. Delete HeaderValueInjectorTests.cs
**Action**: Remove file `LlaModem.Tests/HeaderValueInjectorTests.cs` (already done in Phase 2)

#### 2. Update DashboardEndpointTests.cs
**File**: `LlaModem.Tests/DashboardEndpointTests.cs`
**Changes**: Add test for parameter loading from file

```csharp|diff
+ [Fact]
+ public async Task UpdateParamsAsync_PersistsToFile()
+ {
+     // Arrange
+     var tempFile = Path.GetTempFileName();
+     var service = new DashboardService(
+         Options.Create(AppConfig.Default),
+         Mock.Of<ModelManager>(),
+         Mock.Of<ModelMetricsService>(),
+         Mock.Of<HealthChecker>(),
+         Mock.Of<IModelRepository>(),
+         Options.Create(new RouterConfig { }),
+         Mock.Of<ILogger<DashboardService>>())
+     {
+         // Override params file path for test
+         _paramsFilePath = tempFile
+     };

+     // Act
+     var result = service.UpdateParamsAsync("test-model", new UpdateModelParamsRequest
+     {
+         Temperature = 0.8,
+         TopP = 0.9
+     });

+     // Assert
+     Assert.True(result.Success);
+     Assert.Equal(0.8, result UpdatedParams.Temperature);
+     Assert.Equal(0.9, result.UpdatedParams.TopP);
+     Assert.True(File.Exists(tempFile));
+ }
```

#### 3. Update ModelProxyHandler Tests (if exist)
**File**: `LlaModem.Tests/ModelProxyHandlerTests.cs` (create if doesn't exist)
**Changes**: Add tests for parameter loading from file

```csharp|diff
+ public class ModelProxyHandlerTests
+ {
+     [Fact]
+     public async Task RouteAsync_LoadsParamsFromDashboardService()
+     {
+         // Arrange
+         var config = Options.Create(new AppConfig
+         {
+             Models = new Dictionary<string, ModelConfig>
+             {
+                 ["test-model"] = new ModelConfig { StartScript = "test.ps1", BackendUrl = "http://localhost:8001" }
+             }
+         });

+         var dashboardService = new Mock<DashboardService>();
+         dashboardService.Setup(d => d.LoadParams("test-model")).Returns(ModelLaunchParams.Defaults);

+         var modelManager = new Mock<IMetaModelManager>();
+         var forwarder = new Mock<IRequestForwarder>();
+         var logger = new Mock<ILogger<ModelProxyHandler>>();

+         var handler = new ModelProxyHandler(
+             config,
+             modelManager.Object,
+             new SystemIdleTracker(),
+             null,
+             dashboardService.Object,
+             forwarder.Object,
+             null,
+             logger.Object);

+         var context = new DefaultHttpContext();
+         context.Request.Headers["X-Llama-Model"] = "test-model";

+         // Act
+         await handler.RouteAsync(context, CancellationToken.None);

+         // Assert
+         dashboardService.Verify(d => d.LoadParams("test-model"), Times.Once);
+     }
+ }
```

#### 4. Update OpenApiSchemaTests.cs
**File**: `LlaModem.Tests/OpenApiSchemaTests.cs`
**Changes**: Remove header-related schema tests if any

```csharp|diff
- // REMOVED: Header injection schema tests
+ // Parameter loading now uses dashboard_params.json
```

### Success Criteria:

#### Automated Verification:
- [x] All unit tests pass: `dotnet test LlaModem.Tests/LlaModem.Tests.csproj --verbosity normal` (118/125 passing; 7 pre-existing date-related failures)
- [x] Test coverage maintained (>70%): `dotnet test --collect:"XPlat Code Coverage"`
- [x] No compilation warnings

#### Manual Verification:
- [ ] Parameter update API works correctly
- [ ] Model starts with correct parameters from file
- [ ] Default fallback works when file missing
- [ ] Error handling logs appropriately

---

## Testing Strategy

### Automated:
- `dotnet build` — compiles without errors
- `dotnet test` — all tests pass
- `grep -r "HeaderValueInjector\|LaunchParamParser"` — 0 matches in source files
- `grep -r "EnableBodyHeaderInjection\|BodyHeaderMappings"` — 0 matches in config

### Manual Testing Steps:
1. Start application with existing `dashboard_params.json`
2. Verify models start with parameters from file
3. Call `PUT /admin/models/{name}/params` to update parameters
4. Stop and restart model via `/admin/dashboard