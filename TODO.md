# TODO

## Goal

Fix hardcoded window title and script path in DefaultModelLauncher so each model launches with its own PowerShell window title matching the model name, and update IsModelRunning to correctly identify models by their window titles.

## Tasks

### 1. Fix StartAsync — use dynamic window title and script path

- [x] Replace hardcoded `'qwen-smart'` in `$Host.UI.RawUI.WindowTitle` with the actual `modelName` parameter
- [x] Replace hardcoded `'F:/llama/llama-qwen36-SMART.ps1'` with the actual `scriptPath` parameter (properly escaped)

### 2. Verify IsModelRunning works correctly after title fix

- [x] Confirm `IsModelRunning(string modelName)` already searches by model name in MainWindowTitle — no code change needed, just verify it matches the new window title format
- [x] Update the TODO.md note about the hardcoded `'qwen-smart'` to reflect the fix

## Notes

- The `modelName` parameter is already passed through from `StartAsync` and `IsModelRunning` — only the launcher implementation was ignoring them
- Models defined in appsettings.json: `qwen-smart`, `qwen-fast` — each should have its own uniquely titled PowerShell window
- The `scriptPath` parameter comes from `ModelConfig.StartScript` which is already configured per-model in appsettings.json
- `IsModelRunning` was already correct (uses `modelName` param) — the bug was only in `StartAsync` hardcoding the title
