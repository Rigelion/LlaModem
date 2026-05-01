# Refactoring Plan — LlaModem

## Goal
Improve code quality, testability, and maintainability based on code review findings. Prioritize low-risk, high-impact changes that preserve behavior.

## Prioritized Tasks

### Phase 1: Extract Duplicate Process Lookup (low risk)
- [ ] Extract `FindProcessByTitle` from `DefaultModelLauncher` — deduplicates `IsModelRunning` and `StopModelByNameAsync`

### Phase 2: Simplify SystemIdleTracker concurrency (low risk)
- [ ] Replace lock-based `_lastRequest` with `volatile` field
- [ ] Update tests if needed

### Phase 3: Add failure reasons to HealthChecker (medium risk)
- [ ] Change return type from `bool` to `(bool success, string? reason)`
- [ ] Update all callers: `HealthChecker`, `DefaultModelLauncher.IsModelRunningV2`, `ModelManager`
- [ ] Improve logging in callers

### Phase 4: Fix ModelManager optional parameters (low risk)
- [ ] Remove optional parameters from constructor
- [ ] Register defaults in DI in Program.cs
- [ ] Update test setup if needed

### Phase 5: Static functions for stateless services (SKIPPED)
- [x] Deferred — current DI pattern is testable and idiomatic; converting adds complexity without solving a real problem
- [ ] Update DI registrations and callers

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
