---
type: source
title: "Post-construction callback wiring to break DI cycles"
slug: post-construction-callback-wiring-pattern
status: insight
created: 2026-05-24
updated: 2026-05-24
category: architecture
---
# Post-construction callback wiring to break DI cycles
## Problem

Circular DI dependency between `ModelManager` and `IdleTimeoutService`:
- `ModelManager` depends on `IIdleTimeoutResetter` → resolves to `IdleTimeoutService`
- `IdleTimeoutService` needs a callback to trigger model shutdown when idle timeout fires → needs `ModelManager.StopActiveModelAsync`

This creates a cycle: ModelManager → IdleTimeoutService → ModelManager.

## Solution: Post-Construction Callback Wiring

Instead of injecting the callback directly into the constructor (which creates a DI cycle), use a two-phase approach:

1. **Constructor phase:** Register both services as singletons with minimal dependencies
2. **Post-build phase:** After `builder.Build()`, resolve both services and wire the callback manually

```csharp
// Registration — no circular dependency
builder.Services.AddSingleton<ModelManager>();
builder.Services.AddSingleton<IIdleTimeoutResetter, IdleTimeoutService>();

// Post-build wiring
var modelManager = app.Services.GetRequiredService<ModelManager>();
var idleTimeoutResetter = app.Services.GetRequiredService<IIdleTimeoutResetter>();
if (idleTimeoutResetter is IdleTimeoutService idleService)
{
    idleService.SetIdleStopCallback(modelManager.StopActiveModelAsync);
}
```

## Key Pattern: SetIdleStopCallback Method

Add a public setter method on the service that receives the callback:

```csharp
public void SetIdleStopCallback(Func<CancellationToken, Task> callback)
{
    _stopActiveModel = callback;
}
```

The field can be non-readonly since it's set after construction.

## Why This Works

- DI container resolves each service independently (no circular dependency in constructor signatures)
- Cross-references are established only after the container is fully built
- Both services exist as singletons before wiring occurs
- No need for IHostApplicationLifetime or deferred initialization
*Category: architecture*
---
*Captured: 2026-05-24*
## Related
_Add links to related pages._