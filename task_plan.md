# Task Plan: Add Total Context Size via Headers

## Goal
Parametrize total context size (`contextLength`) as a launch parameter only, passed via the `X-Llama-ContextLength` header, which maps to the `-c` flag on the model launch script.

## Phases

### Phase 1: Add Header Constant
- [x] Add `ContextLength = "X-Llama-ContextLength"` to `ProxyHeaders.cs`
- Status: **complete**

### Phase 2: Add to ModelLaunchParams Record
- [ ] Add `int? ContextLength` to `ModelLaunchParams` record
- Status: **pending**

### Phase 3: Parse Header in LaunchParamParser
- [ ] Parse `X-Llama-ContextLength` as integer in `LaunchParamParser.ParseAsync()`
- Status: **pending**

### Phase 4: Pass to Launch Script
- [ ] Add `-ContextLength` argument in `DefaultModelLauncher.StartAsync()`
- Status: **pending**

### Phase 5: Log Header in RequestLoggingMiddleware
- [ ] Log `X-Llama-ContextLength` in request log output
- Status: **pending**

### Phase 6: Tests
- [ ] Add tests for parsing `X-Llama-ContextLength` header
- [ ] Add tests for `ModelLaunchParams` with `ContextLength`
- Status: **pending**

### Phase 7: Verify Build
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- Status: **pending**

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| — | — | — |

## Notes
- This is a launch parameter only (not body injection) — aligns with previous session decision for `max_tokens`
- Header value is an integer (token count)
- Maps to `-c` flag on the PowerShell launch script
