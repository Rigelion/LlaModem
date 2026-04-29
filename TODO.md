# TODO

## Goal

Replace the current basic logging with Serilog, add rolling file support, and create a request logging middleware that logs full incoming request details for all endpoints.

## Tasks

### 1. Add Serilog Dependencies

- [x] Add `Serilog`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Async`, `Serilog.Sinks.File`, and `Serilog.Sinks.Console` package references to `LlaModem.csproj`
- [x] Add Serilog-related settings to `appsettings.json`: `Serilog.MinimumLevel` (default "Debug"), `Serilog.WriteTo.Console`, `Serilog.WriteTo.File` (path, rolling enabled, maxFileSize, retainedFileCountLimit)

### 2. Create Serilog Configuration in Program.cs

- [x] Replace `WebApplication.CreateBuilder(args)` default logger with Serilog-based logging in `Program.cs`
- [x] Configure Serilog to read settings from `appsettings.json` (`MinimumLevel`, Console sink, Async File sink with rolling)
- [x] Wire Serilog as the logging provider via `builder.Host.UseSerilog()`

### 3. Create RequestLoggingMiddleware

- [x] Create `Middleware/RequestLoggingMiddleware.cs` with a class that implements `InvokeAsync(HttpContext)`
- [x] Log at `Information` level: HTTP method, scheme, path, query string, client IP, `X-Llama-Model` header (if present), Authorization header presence (but NOT the credentials)
- [x] Log at `Debug` level: full request body (read and buffer it, then re-enable the body stream for downstream consumption)
- [x] Log at `Information` level after response: status code, elapsed time (ms), total request size
- [x] Handle body buffering carefully: use `DisableBuffering()` + re-enable buffering so downstream code can still read the body

### 4. Wire Up Middleware and Register in Pipeline

- [x] Create `Middleware/RequestLoggingExtensions.cs` with an extension method `UseRequestLogging()`
- [x] Register the middleware in `Program.cs` **before** `UseBasicAuthWhen` so request logs capture all requests including auth failures
- [x] Ensure the middleware is applied to all routes (not scoped to a path prefix)

### 5. Test and Verify

- [x] Build the project to confirm no compilation errors
- [ ] Run the application and verify: console logs show request details at Debug level, rolling log file is created under `logs/` directory, body content appears in debug-level output

## Notes

- The middleware must re-enable request body buffering after reading it, so downstream endpoints can still consume the body
- Authorization header presence should be logged (e.g., "Auth: present/absent") but NEVER the actual credentials
- Serilog async sink should be used for file writing to avoid blocking request processing
- Minimum log level is Debug, so Information-level logs (method, path, status, latency) will always appear
