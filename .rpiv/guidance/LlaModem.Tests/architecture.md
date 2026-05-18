# LlaModem.Tests

## Test Project Overview

Unit test project using xUnit framework. Focuses on service logic, middleware behavior, and utility functions with in-memory mocks.

## Module Structure

```
LlaModem.Tests/
├── DashboardEndpointTests.cs      — Admin endpoint handlers (status, stats)
├── ErrorResponseWriterTests.cs    — Structured error responses
├── HeaderValueInjectorTests.cs   — HTTP header to JSON body mapping
├── HttpConstantsTests.cs         — Route prefixes, header names
├── HybridRoutingTests.cs         — Model routing logic
├── IdleTimeoutServiceTests.cs    — Background idle shutdown service
├── InMemoryModelRepositoryTests.cs — Process state repository
├── LaunchParamParserTests.cs     — Launch parameter parsing
├── Middleware/
│   └── BasicAuthMiddlewareTests.cs — Auth validation tests
├── ModelPricingTests.cs          — Cloud cost comparison pricing
├── OpenApiSchemaTests.cs         — OpenAPI document transformations
├── RequestForwarderTests.cs      — HTTP proxy logic
├── StatsServiceTests.cs          — Usage aggregation queries
├── UsageCaptureMiddlewareTests.cs — Response usage extraction
├── UsageServiceTests.cs          — Session entry recording
└── LlaModem.Tests.csproj        — Test project configuration
```

## Testing Strategy

### In-Memory Mocks

**IModelRepository:** `InMemoryModelRepository` (same as production)
- Used for unit tests without external dependencies
- Thread-safe dictionary keyed by model name

**IUsagePersistence:** Fake persistence implementations
- In-memory storage for usage data
- Verify recording calls with correct parameters

**HttpClient mocks:** `HttpClientFactory` with `MockHttpMessageHandler`
- Simulate backend responses (success, error, timeout)
- Test error handling paths

### Focus Areas

| Area | Tested | Not Tested |
|------|--------|------------|
| Service logic | ✅ Pure functions, state management | ❌ HTTP routing |
| Middleware behavior | ✅ Request/response processing | ❌ Endpoint registration |
| Utility functions | ✅ Header parsing, JSON extraction | ❌ DI container setup |
| Configuration binding | ✅ Record validation | ❌ appsettings.json loading |

### Test Organization

**By component:** Tests grouped by service/middleware under test.

**By scenario:** Each test file covers one component's public API surface.

## Key Test Patterns

### Dependency Injection Setup

```csharp
public class StatsServiceTests
{
    private readonly ServiceCollection _services = new();
    
    [Fact]
    public void Test_Method_WhenCondition_ThenResult()
    {
        // Arrange
        _services.AddSingleton<IUsagePersistence, InMemoryUsagePersistence>();
        var provider = _services.BuildServiceProvider();
        var service = provider.GetRequiredService<StatsService>();
        
        // Act
        var result = await service.GetDailyUsageAsync(30, "qwen36-smart");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.Count);
    }
}
```

### HttpContext Mocking

```csharp
public class RequestForwarderTests
{
    private DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":\"test\"}"));
        context.Request.ContentType = "application/json";
        return context;
    }
}
```

### Fake Persistence

```csharp
public class InMemoryUsagePersistence : IUsagePersistence
{
    private readonly List<SessionEntry> _entries = [];
    
    public Task Insert(SessionEntry entry)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }
    
    public Task<IReadOnlyList<SessionEntry>> GetEntriesAsync(...)
    {
        return Task.FromResult<IReadOnlyList<SessionEntry>>(_entries.AsReadOnly());
    }
}
```

## Test Coverage Focus

### High Priority (Well Tested)

| Component | Coverage | Notes |
|-----------|----------|-------|
| `UsageExtractor` | ✅ Full | JSON parsing, edge cases |
| `HeaderValueInjector` | ✅ Full | Type conversion, mappings |
| `ModelManager` | ✅ Core paths | Lazy start, health check failures |
| `StatsService` | ✅ Aggregation | Daily usage, pagination |

### Medium Priority (Partially Tested)

| Component | Coverage | Notes |
|-----------|----------|-------|
| `RequestForwarder` | ✅ Success path | Backend errors mocked |
| `DashboardService` | ✅ GET endpoints | POST endpoints minimal |
| `IdleTimeoutService` | ✅ Trigger logic | Background service hard to test |

### Low Priority (Minimal Testing)

| Component | Coverage | Notes |
|-----------|----------|-------|
| `DefaultModelLauncher` | ❌ None | PowerShell process hard to mock |
| `HealthChecker` | ⚠️ Polling logic | Time-based tests flaky |
| `GpuMemoryChecker` | ⚠️ nvidia-smi parsing | System dependency |

## Running Tests

```bash
dotnet test LlaModem.Tests/LlaModem.Tests.csproj
dotnet test --logger "console;verbosity=detailed"
dotnet test --filter "Category=Integration"
```

**Test discovery:** xUnit auto-discovers `[Fact]` and `[Theory]` attributes.

## Continuous Integration

**GitHub Actions:** Tests run on every PR to `develop` branch.

**CI pipeline:**
1. `dotnet restore` — Restore NuGet packages
2. `dotnet build` — Build test project
3. `dotnet test` — Run all tests
4. Report coverage (if configured)

## Adding New Tests

### For Services

1. Create test class in `LlaModem.Tests/`: `[ClassName]Tests.cs`
2. Use `[Fact]` for unit tests, `[Theory]` for parameterized tests
3. Mock dependencies via constructor injection or fake implementations
4. Assert on return values and side effects (logging, persistence calls)

### For Middleware

1. Create test class in `LlaModem.Tests/Middleware/`: `[MiddlewareName]Tests.cs`
2. Use `DefaultHttpContext` for request/response mocking
3. Verify status codes, headers, and body content
4. Test both success and error paths

### For Utilities

1. Create test class in root `LlaModem.Tests/`: `[UtilityClass]Tests.cs`
2. Test pure functions with various inputs
3. Assert on return values (no side effects to mock)
4. Include edge cases (null, empty, invalid)

## Test Data Fixtures

**JSON responses:** Store llama-server JSON in test fixtures or inline strings.

```csharp
private const string LlamaServerResponse = @"{
  ""choices"": [{ ""finish_reason"": ""stop"", ""text"": ""Hello"" }],
  ""usage"": {
    ""prompt_tokens"": 150,
    ""completion_tokens"": 300,
    ""total_tokens"": 450
  }
}";
```

**Session entries:** Create `SessionEntry` instances directly for aggregation tests.

## Critical Notes

- **No integration tests:** HTTP endpoints not tested via actual server
- **PowerShell scripts:** Not tested (external process dependency)
- **Real SQLite:** Tests use in-memory persistence, not actual database file
- **Async testing:** All tests use `async/await` with `Task` return types

## Workflow: Debugging Test Failures

1. Run specific test: `dotnet test --filter "FullyQualifiedName~TestName"`
2. Enable verbose logging: Add `logger.Setup()` in test setup
3. Check test output: View failed assertion messages in test explorer
4. Reproduce manually: Copy test logic to console app for debugging
