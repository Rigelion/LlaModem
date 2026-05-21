---
type: source
title: "Idle timer fires immediately due to zero-initialized LastRequest"
slug: idle-timer-epoch-zero-bug
status: insight
created: 2026-05-21
updated: 2026-05-21
category: bugfix
---
# Idle timer fires immediately due to zero-initialized LastRequest
## Problem

`SystemIdleTracker._lastRequestTicks` was a field with no constructor, defaulting to `0` (Unix epoch = 1970-01-01). When `IdleTimeoutService` started its background polling loop, every tick computed:

```
elapsed = UtcNow - LastRequest  // UtcNow - 1970-01-01 ≈ 56 years
```

This vastly exceeded any `IdleTimeoutSeconds` value, causing immediate model shutdown on the first timer tick. The shutdown was a no-op at startup (no active model), but would fire immediately once a model became active.

## Fix

Added a constructor to `SystemIdleTracker` that seeds `_lastRequestTicks` with `UtcNow.ToUnixTimeMilliseconds()`. First tick now sees `elapsed ≈ 0`.

## Files

- `Services/SystemIdleTracker.cs` — added constructor with epoch seed
- `LlaModem.Tests/IdleTimeoutServiceTests.cs` — no changes needed (constructor is additive)

## Lessons

- Zero-initialized numeric fields in interlocked/shared state are a ticking timebomb. Always explicitly seed timestamps.
- `BackgroundService` starts immediately on DI resolution — its first tick can fire before any application "ready" signal.
*Category: bugfix*
---
*Captured: 2026-05-21*
## Related
_(Add [[wikilinks]] to related pages)_