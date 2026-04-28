# TODO

## Goal

Fix the false "insufficient VRAM" error on subsequent requests by adding a backend URL health check as an additional process detection mechanism, and move the VRAM check to only fire when we're truly starting fresh.

## Tasks

### 1. Force new PowerShell window for model output

- [x] In `DefaultModelLauncher.StartAsync()`, set `UseShellExecute = true` so the PowerShell process opens its own console window and outputs go there (not captured by parent)

### 2. Add IsModelRunningV2 — backend URL health check

- [x] Add `IsModelRunningV2(string backendUrl)` method to `DefaultModelLauncher` that checks if the backend URL responds to a health check (GET `/health`), returning true if the model's llama-server is actually running and healthy

### 2. Fix EnsureModelAsync — only check VRAM when truly starting fresh

- [x] In `EnsureModelAsync`, add a second early-exit check using `IsModelRunningV2` that verifies the backend URL is responding before attempting to start
- [x] Move the VRAM resource check from `StartModelAsync` into a path that only executes when we're certain no model process is running (i.e., only after both `IsModelRunning` and `IsModelRunningV2` return false)
- [x] If either `IsModelRunning` or `IsModelRunningV2` returns true, skip starting and just return as already active

## Notes

- `IsModelRunning` (PowerShell window title check) stays as-is with hardcoded `'qwen-smart'`
- `IsModelRunningV2` is added but not yet used — user will test it first before enabling it in the flow
- VRAM check should only trigger when we're actually launching a new model, not when an existing one is already running
- `UseShellExecute = true` means stdout/stderr capture via `OutputDataReceived` won't work — those handlers can be removed or left as no-op
