# LlaModem — Pre-Release Refactoring TODO

## 🔴 Critical (fix before release)

### 1. ~~Remove blocking async call in `ModelManager.Dispose()`~~ ✅
**File:** `Services/ModelManager.cs`
**Problem:** `StopActiveModelAsync().GetAwaiter().GetResult()` blocks on async code in a synchronous Dispose — deadlock risk with ASP.NET Core's SynchronizationContext.
**Fix:** Removed `Dispose()` and `IDisposable` interface. DI container handles lifecycle; `Program.cs` calls `launcher.ShutdownAllAsync()` on shutdown.

### 2. Fix `RequestLoggingMiddleware` response body buffering
**File:** `Middleware/RequestLoggingMiddleware.cs`
**Problem:** Buffers entire response body in a MemoryStream — breaks streaming (SSE/chunked), which is the primary LLM use case. Holds all data in memory until client disconnects.
**Fix:** Only buffer for non-streaming responses, or skip response body buffering entirely and log status code + duration after `_next(context)` returns.

### 3. Fix hardcoded Windows PowerShell path
**File:** `Services/DefaultModelLauncher.cs`
**Problem:** `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` won't work on 32-bit Windows, non-standard installs, or in tests.
**Fix:** Use `"powershell"` and let OS resolve via PATH, or resolve at startup via `Environment.GetFolderPath(Environment.SpecialFolder.System)`, or make configurable.

### 4. Fix `HealthChecker` creating new HttpClient per call
**File:** `Services/HealthChecker.cs`
**Problem:** Both `CheckAsync` and `PollAsync` create fresh clients via `_httpClientFactory.CreateClient()`. Polling every 500ms generates unnecessary garbage.
**Fix:** Create one HttpClient in `PollAsync` and reuse it across the polling loop, or inject a named/pooled HttpClient for health checks.

---

## 🟡 High Priority (fix before release)

### 5. Fix double-read of request body in middleware pipeline
**Files:** `Middleware/RequestLoggingMiddleware.cs`, `Services/HeaderValueInjector.cs`
**Problem:** `RequestLoggingMiddleware` buffers the body, then `HeaderValueInjector` calls `EnableBuffering()` again — redundant and signals uncertainty about middleware ordering.
**Fix:** Have `HeaderValueInjector` rely on the already-buffered stream without calling `EnableBuffering()`. Add a comment or extract a shared `IBodyReader` abstraction.

### 6. Move `ExcludedHeaders` out of `EndpointSetup`
**Files:** `EndpointSetup.cs`, `Services/RequestForwarder.cs`
**Problem:** `internal static readonly HashSet<string> ExcludedHeaders` in `EndpointSetup` is accessed by `RequestForwarder`, coupling forwarding to endpoint registration and preventing reuse/testing.
**Fix:** Move to a dedicated static class like `HttpConstants` or inject as a dependency.

### 7. Extract duplicate `WriteErrorAsync` methods
**Files:** `Services/ModelProxyHandler.cs`, `Services/LaunchParamParser.cs`
**Problem:** Both classes have identical `WriteErrorAsync(HttpContext, int, string, string)` methods. Changing error response format requires two edits.
**Fix:** Extract to a shared static class or `HttpContext` extension method.

### 8. Reduce `ModelManager` dependencies (SRP violation)
**File:** `Services/ModelManager.cs`
**Problem:** Constructor takes 7 parameters — manages state, orchestrates launching, checks GPU, handles health polling, coordinates process lifecycle. Too many responsibilities.
**Fix:** Consider splitting into `ModelStateManager` (state, switching) and `ModelOrchestrator` (GPU check → launch → health poll). Larger refactor but significantly improves testability.

### 9. Make `RequestTracker._lock` readonly
**File:** `Services/RequestTracker.cs`
**Problem:** `private object _lock = new();` — not readonly, signals mutability that isn't intended.
**Fix:** `private readonly object _lock = new();`

---

## 🟢 Medium Priority (nice to have)

### 10. Replace magic numbers with configuration
**Files:** `Services/HealthChecker.cs`, `Services/GpuMemoryChecker.cs`, `Services/ProcessKiller.cs`, `Services/IdleTimeoutService.cs`
**Problem:** Hardcoded values: 3s timeout, 5min poll timeout, 12GB VRAM, 5000ms graceful shutdown, 30s timer interval.
**Fix:** Move configurable values to `RouterConfig`. Per-model VRAM threshold would be ideal but is a larger change.

### 11. Defer environment variable expansion in `ModelConfig.StartScript`
**File:** `Config/ModelConfig.cs`
**Problem:** `Environment.ExpandEnvironmentVariables(value)` runs at config binding time — can't be tested without real env vars, couples config loading to runtime state.
**Fix:** Expand at read time, or expand once in `Program.cs` during startup and store the expanded value.

### 12. Add test project with basic coverage
**Files:** New — `LlaModem.Tests/`
**Problem:** Well-designed interfaces but no tests. `.csproj` has `<Compile Remove="LlaModem.Tests\**" />` suggesting a planned but uncreated test project.
**Fix:** Add unit tests for:
- `HeaderValueInjector` (pure logic, easy to test)
- `LaunchParamParser` (parsing logic)
- `RequestForwarder.BuildTargetUrl` (static method, pure function)
- `GpuMemoryChecker.FormatBytes` (formatting logic)

### 13. Sanitize username in BasicAuth log messages
**File:** `Middleware/BasicAuthMiddleware.cs`
**Problem:** Logs plaintext username on auth failure: `"Authentication failed for user '{User}'"`. Visible in centralized logging systems.
**Fix:** Hash or truncate username in log output.

---

## Recommended Execution Order

1. **#1** — Blocking async in Dispose (shutdown hang risk)
2. **#2** — Response body buffering (breaks streaming, primary use case)
3. **#3** — PowerShell path (could fail on non-standard installs)
4. **#4** — HttpClient reuse (resource waste during model startup)
5. **#7** — Extract shared WriteErrorAsync (quick win, eliminates duplication)
6. **#6** — Move ExcludedHeaders (reduces coupling)
7. **#12** — Add basic test project (gives confidence for further changes)

> "If it hurts, do it more often." — Pay down debt in high-activity areas first. The cruft in stable areas doesn't trigger interest until you touch them.
