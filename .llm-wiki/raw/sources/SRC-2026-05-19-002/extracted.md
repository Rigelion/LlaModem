# Remove Header-to-Body Parameter Injection System
# Implementation Summary

Successfully removed the HTTP header-to-body parameter injection system and switched to file-based parameter loading via `dashboard_params.json`.

## Key Changes

### 1. Architecture Simplification
- Removed `HeaderValueInjector` service (no longer needed)
- Removed `LaunchParamParser` service (replaced by direct file loading)
- Cleaned up `RouterConfig` - removed `EnableBodyHeaderInjection` and `BodyHeaderMappings` options
- Simplified `RequestForwarder` constructor and removed injection call

### 2. Parameter Type Precision Improvement
- Changed all model launch parameters from `double?` to `decimal?` for exact 2-decimal precision
- llama-server requires precise decimal values (e.g., 0.6 not 0.6000000000000001)
- Updated `ModelLaunchParams.Defaults` with `'m` suffix literals
- Modified `DashboardService.TryGetDecimal()` to use `Math.Round(value.GetDecimal(), 2)`

### 3. New Parameter Loading Flow
```csharp
// Old: Parse HTTP headers
var launchParams = await _paramParser.ParseAsync(context, request);

// New: Load from persisted JSON file
var launchParams = _dashboardService.LoadParams(modelName);
```

## Files Modified
- `Config/RouterConfig.cs` - Removed injection config options
- `Services/RequestForwarder.cs` - Simplified constructor, removed injection call
- `Services/ModelProxyHandler.cs` - Added `LoadParamsForModel()` method using DashboardService
- `Services/DashboardService.cs` - Made `LoadParams()` public, updated `TryGetDecimal()`
- `Services/ModelLaunchParams.cs` - Changed types to `decimal?`, added `'m` suffixes
- `powershell/*.ps1` - Updated param blocks to `[decimal]`, direct variable interpolation
- `Program.cs` - Removed DI registrations for deleted services

## Files Deleted
- `Services/HeaderValueInjector.cs`
- `Services/LaunchParamParser.cs`

## Testing Notes
- Build passes: 0 warnings, 0 errors
- Unit tests: 118/125 passing (94.4%)
- 7 pre-existing failures in `StatsServiceTests` (date-related, unrelated)

## Decisions Made
1. **No automatic restart on param change** - Model continues with old values until next start
2. **Direct PowerShell interpolation** - Variables like `$Temperature` work correctly without `.ToString()`
3. **Silent fallback to defaults** - Exceptions in `LoadParamsForModel()` log warning and use defaults

## Related Wiki Pages
- [[Services/ModelProxyHandler]]
- [[Services/DashboardService]]
- [[Config/architecture]]
- [[powershell/README]]

---

**Date**: 2026-05-19  
**Author**: Rigelion  
**Plan**: `.rpiv/artifacts/plans/2026-05-19_14-30-00_remove-header-body-injection.md`
---
*Captured: 2026-05-19*
*Category: architecture*