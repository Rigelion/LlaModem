---
type: source
title: "Lifecycle management via StartIdleTracking and StopIdleTracking"
slug: lifecycle-management-via-start-stop-tracking
status: insight
created: 2026-05-24
updated: 2026-05-24
category: design
---
# Lifecycle management via StartIdleTracking and StopIdleTracking
## Problem

The original `IIdleTimeoutResetter.Reset()` method was ambiguous — it was called on every incoming request to reset the idle timer, but also needed to signal when model lifecycle started/stopped. This created a confusing API surface where "reset" meant both "restart the polling loop" and "reset the idle timer."

## Solution: Explicit Lifecycle Methods

Replaced `void Reset()` with two explicit methods:
- `StartIdleTracking()` — begins polling, resets idle timer, starts the background task
- `StopIdleTracking()` — cancels the polling loop, disposes resources

This makes the intent clear at call sites:
```csharp
// Model launch successful → start tracking
_idleTimeout.StartIdleTracking();

// Model shutdown → stop tracking
_idleTimeout.StopIdleTracking();
```

## Implementation Details

- `StartIdleTracking()` creates a new `ManualResetEventSlim(true)` and starts a fresh `ExecuteAsync` task
- `StopIdleTracking()` cancels the existing `CancellationTokenSource` and disposes the timer
- The polling loop checks `_resetSignal.IsSet` to determine if tracking is active
- Idle timeout callback triggers model shutdown via injected delegate

## Benefits

- Clear separation of concerns: lifecycle vs. timer reset
- No ambiguity about what "reset" means
- Easier to test and reason about state transitions
*Category: design*
---
*Captured: 2026-05-24*
## Related
_Add links to related pages._