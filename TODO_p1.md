# Phase 1: Project File & Solution Upgrade

## Goal
Update the project file and solution to target .NET 10.

**All tasks complete ✅**

## Tasks

### 1.1 Update csproj
- [x] Change `<TargetFramework>` from `net8.0` to `net10.0` in `LlaModem.csproj`
- [x] Update `Microsoft.AspNetCore.OpenApi` package reference from `8.0.*` to `10.0.*`

### 1.2 Update solution file
- [x] Update VisualStudioVersion and MinimumVisualStudioVersion in `LlaModem.sln` for .NET 10 compatibility

## Notes
- No source code changes expected — Minimal API, DI, middleware are all stable across versions
