# Progress Log: Total Context Size via Headers

## Session Log

### 2026-05-08 — Session Start
- **Started**: Planning phase for adding `contextLength` launch parameter
- **Context recovered**: Previous session was working on `max_tokens` as launch param (not body injection)
- **Current state**: Phase 1 complete (ProxyHeaders.cs updated)
- **Git diff**: 1 file changed, 1 insertion

### Phase 1: Add Header Constant — COMPLETE
- File: `Config/ProxyHeaders.cs`
- Added: `public const string ContextLength = "X-Llama-ContextLength";`
- Status: ✅ Done

### Phase 2: Add to ModelLaunchParams Record — PENDING
- File: `Services/ModelLaunchParams.cs`
- Action: Add `int? ContextLength` field

### Phase 3: Parse Header — PENDING
- File: `Services/LaunchParamParser.cs`
- Action: Parse `X-Llama-ContextLength` as int

### Phase 4: Pass to Launch Script — PENDING
- File: `Services/DefaultModelLauncher.cs`
- Action: Add `-ContextLength` to paramParts

### Phase 5: Log Header — PENDING
- File: `Middleware/RequestLoggingMiddleware.cs`
- Action: Add context length to log output

### Phase 6: Tests — PENDING
- File: `LlaModem.Tests/LaunchParamParserTests.cs`
- Action: Add tests for ContextLength parsing

### Phase 7: Verify — PENDING
- Run `dotnet build` and `dotnet test`
