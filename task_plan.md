# Task Plan: Unify Ports & Simplify Model Config

## Goal
1. Single backend URL: `http://localhost:8001` (router stays at 9000)
2. Remove per-model backend URL — use one shared backend
3. Remove `qwen-smart` / `qwen-fast` model entries from config
4. PowerShell window title becomes a constant (no per-model name tracking)
5. Leave `QWEN_SMART_START_SCRIPT` / `QWEN_FAST_START_SCRIPT` env var locations as-is

## Current State
- Two models: `qwen-smart` (port 8001) + `qwen-fast` (port 8002)
- `RouterConfig.ListenUrl` = `http://localhost:9000` (keep)
- `ModelConfig.BackendUrl` per-model
- PowerShell window title = model name (used for process discovery)
- `AppConfig.Models` = `Dictionary<string, ModelConfig>`

## Phases

### Phase 1: appsettings.json — Remove dual models, single backend
- [ ] Remove `qwen-smart` and `qwen-fast` model entries
- [ ] Add single `default` model entry with `StartScript` (same as qwen-smart's)
- [ ] Add top-level `BackendUrl: "http://localhost:8001"` — move out of per-model config
- **Status:** not started

### Phase 2: AppConfig — Move BackendUrl out of per-model
- [ ] Add `BackendUrl` property to `AppConfig` (string, init)
- [ ] Remove `BackendUrl` from `ModelConfig`
- [ ] Update all `ModelConfig.BackendUrl` references → `AppConfig.BackendUrl`
- **Status:** not started

### Phase 3: Constant PowerShell title
- [ ] Replace `modelName` in PowerShell window title with constant `"LlaModem"`
- [ ] Update `IsModelRunning` → check for `"LlaModem"` title instead of model name
- [ ] Update `FindProcessByTitle` → use constant title
- [ ] `StopModelByNameAsync` → keep for compatibility but search by constant title
- **Status:** not started

### Phase 4: Update all consumers
- [ ] `Program.cs` — update env var logging (remove per-model vars, keep both locations)
- [ ] `ModelProxyHandler.cs` — simplify error messages, remove per-model config lookup
- [ ] `ModelManager.cs` — simplify `EnsureModelAsync` (no per-model config needed)
- [ ] `RequestForwarder.BuildTargetUrl` — use `AppConfig.BackendUrl` instead of `ModelConfig.BackendUrl`
- [ ] `EndpointSetup.cs` — update admin/status endpoints
- [ ] `DefaultModelLauncher` — update method signatures where `modelName` is no longer needed
- **Status:** not started

### Phase 5: Tests
- [ ] Update all test files referencing `qwen-smart` / `qwen-fast` / per-model config
- [ ] Update mock setups for `AppConfig`
- **Status:** not started

### Phase 6: Build & Test
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all tests pass
- **Status:** not started

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
|       |         |            |

## Implementation Progress

### Phase 1: appsettings.json
**Status:** ✅ Done

### Phase 2: AppConfig — Move BackendUrl out of per-model
**Status:** ✅ Done

### Phase 3: Constant PowerShell title
**Status:** ✅ Done

### Phase 4: Update all consumers
**Status:** ✅ Done

### Phase 5: Tests
**Status:** ✅ Done

### Phase 6: Build & Test
**Status:** ✅ Done
- Build: 0 warnings, 0 errors
- Tests: 50 passed, 0 failed
