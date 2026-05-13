# Idle Timeout Improvements

## Overview

Improved idle timeout behavior by restarting `IdleTimeoutService` on each `/v1/*` request, with clear console output showing service start/stop events.

---

## Problem

The `IdleTimeoutService` BackgroundService ran continuously from app startup without clear start/stop logging:

```
Idle timeout service started (timeout: 300s)
... (runs until shutdown) ...
Idle timeout service stopped
```

This made it difficult to track when the timer was actually active.

---

## Solution

### Architecture Changes

**1. IIdleTimeoutResetter Interface**
```csharp
public interface IIdleTimeoutResetter
{
    void Reset();
}
```

**2. ManualResetEventSlim Signal Pattern**
- Service waits on signal before starting timer
- `Reset()` method disposes and recreates the service
- Clear console output on each restart

**3. Path-Based Reset Trigger**
```csharp
var path = context.Request.Path.Value ?? string.Empty;
if (path.StartsWith("/v1", StringComparison.Ordinal) && !path.StartsWith("/admin", StringComparison.Ordinal))
{
    _idleTimeoutResetter?.Reset();
}
```

---

## Implementation Details

### Files Modified

| File | Changes |
|------|---------|
| `Services/IdleTimeoutService.cs` | Added IIdleTimeoutResetter, ManualResetEventSlim, Reset() method |
| `Services/ModelProxyHandler.cs` | Injected IIdleTimeoutResetter, path matching logic |

### Key Code Changes

**IdleTimeoutService.Reset():**
```csharp
public void Reset()
{
    _logger.LogInformation("Idle timeout service stopped");
    Dispose();
    _logger.LogInformation("Idle timeout service started (timeout: {Seconds}s)", _config.Timeouts.IdleTimeoutSeconds);
    _timer = new PeriodicTimer(TimeSpan.FromSeconds(CheckInterval));
    _resetSignal = new ManualResetEventSlim(true); // Recreate signal after disposal
}
```

**ModelProxyHandler path matching:**
- `/v1/*` routes trigger reset
- `/admin/*` routes excluded (not user traffic)
- Case-sensitive comparison using `StringComparison.Ordinal`

---

## Test Coverage

**File:** `LlaModem.Tests/IdleTimeoutServiceTests.cs`  
**Total Tests:** 19 (all passing)

| Category | Tests | Description |
|----------|-------|-------------|
| Path matching logic | 12 | /v1/*, /admin/*, edge cases |
| IIdleTimeoutResetter interface | 1 | Interface implementation verified |
| ManualResetEventSlim signal | 1 | Initial state validation |
| Null safety | 1 | Handler with null resetter |
| Reset method | 2 | Timer recreation, disposal |

---

## Expected Console Output

**Before:**
```
Idle timeout service started (timeout: 300s)
... (no clear restart events) ...
Idle timeout service stopped
```

**After:**
```
Idle timeout service started (timeout: 300s)
... user makes /v1/* request ...
Idle timeout service stopped
Idle timeout service started (timeout: 300s)
... (timer restarted, clear visibility) ...
```

---

## Decision Rationale

| Decision | Rationale |
|----------|-----------|
| Restart service on /v1/* requests | Clearer console output, easier to reason about timer state |
| Use ManualResetEventSlim | Lightweight .NET primitive, no extra interfaces needed |
| Exclude /admin/* from reset | Admin ops aren't user traffic, shouldn't prevent shutdown |

---

## Git Commits

| Commit | Description | Files Changed |
|--------|-------------|---------------|
| `a624a02` | feat: restart idle timeout service on /v1/* requests | 2 files, +32 -3 |
| `9658501` | test: add idle timeout service tests | 2 files, +203 -2 |

---

## Usage Notes

- Timer resets on any `/v1/*` API request (e.g., `/v1/chat/completions`, `/v1/embeddings`)
- Admin routes (`/admin/model`, `/admin/stop`) do not affect the timer
- Console logs clearly show each restart event for debugging

---

## Related Documentation

- [Admin Dashboard API](./admin-dashboard-api.md) - Admin route reference
- [PowerShell Parameters](./powershell-parameters.md) - Model launch parameters
