---
type: source
title: "IdleTimeoutService two-instance DI bug fix"
slug: idle-service-two-instance-bug-fix
status: insight
created: 2026-05-24
updated: 2026-05-24
category: bugfix
---
# IdleTimeoutService two-instance DI bug fix
## Problem

`IdleTimeoutService` was registered twice in the DI container:
1. As singleton: `builder.Services.AddSingleton<IIdleTimeoutResetter, IdleTimeoutService>()`
2. As hosted service: `builder.Services.AddHostedService<IdleTimeoutService>()`

This caused two separate instances to be created:
- **Instance A** (singleton): Injected into `ModelManager`, controlled by lifecycle methods
- **Instance B** (hosted service): Ran `ExecuteAsync()` polling loop independently

The bug manifested as `ModelProxyHandler` calling `Reset()` on Instance A while the polling loop ran on Instance B — no coordination between idle tracking and model shutdown.

## Fix

Removed `AddHostedService<IdleTimeoutService>()` from `Program.cs`. The service is now explicitly controlled by `ModelManager`:
- `StartIdleTracking()` called after successful model launch
- `StopIdleTracking()` called before/during model shutdown

## Key Files

- `Program.cs` — removed AddHostedService registration
- `Services/IdleTimeoutService.cs` — implements IIdleTimeoutResetter with explicit lifecycle methods
- `Services/ModelManager.cs` — calls StartIdleTracking/StopIdleTracking at appropriate lifecycle points
*Category: bugfix*
---
*Captured: 2026-05-24*
## Related
_Add links to related pages._