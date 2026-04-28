# TODO

## Goal

Align all PowerShell-related calls to use a single shared `powershell.exe` resolution logic, and fix the process stop logic to kill child processes (tree kill).

## Tasks

### 1. Align PowerShell resolution — always use powershell.exe

- [x] Remove `PowerShellResolver.cs` entirely (it's superseded by the unified approach)
- [x] Replace the old `ResolvePowerShellExe()` method with a simple `const string PowerShellExe = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";`
- [x] Update `DefaultModelLauncher.StartAsync()` to use the unified constant
- [x] Update `DefaultModelLauncher.IsModelRunning()` to only check `Process.GetProcessesByName("powershell")` (remove pwsh)

### 2. Fix process stop logic — kill child processes tree

- [x] In `DefaultModelLauncher.StopAsync()`, before killing the main PowerShell process, enumerate and kill all descendant processes (children, grandchildren, etc.) of the process being stopped
- [x] Use `Win32_Process` WMI queries to find and kill child/grandchild processes recursively
- [x] After killing all children, kill the parent PowerShell process gracefully (5s wait), then force kill if needed

## Notes

- Target environment is Windows — do not validate by running on this machine
- Window title in StartAsync arguments remains hardcoded as `"qwen-smart"`
- The hardcoded script path and model name in `StartAsync` arguments are NOT changed yet
- `nvidia-smi` logic in `GpuMemoryChecker` is out of scope
