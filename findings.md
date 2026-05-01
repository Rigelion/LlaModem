# Findings — Refactoring

## Code Review Summary (2026-05-01)

### Key Issues Identified (10 items)

1. **RouterConfig bloat** — holds listen URL, auth, injection settings, and nested TimeoutConfig (8 properties). Should be split into separate config sections.
2. **SystemIdleTracker overuse of lock** — simple volatile/Interlocked would suffice for a single 16-byte field.
3. **HealthChecker swallows exceptions** — silent failure, caller gets no diagnostic info.
4. **Duplicate process lookup** in DefaultModelLauncher — `IsModelRunning` and `StopModelByNameAsync` share identical Process.GetProcessesByName + MainWindowTitle.Contains logic.
5. **ModelManager optional parameters with fallbacks** — hides concrete dependencies, conflates DI with factory logic.
6. **HeaderValueInjector type coercion footgun** — silently guesses bool/int/double from strings.
7. **IdleTimeoutService.CheckInterval magic numbers** — 20/15/60 without named constants.
8. **Request body read 3 times** — fragile pipeline, could benefit from dedicated buffer middleware.
9. **GpuMemoryChecker comment says "on Windows"** but uses nvidia-smi (also works on Linux).
10. **Stateless services wrapped in interfaces** — IHealthChecker, IGpuMemoryChecker, IProcessKiller have no state; static functions + thin adapter would be more idiomatic for this F#-style project.

### Design Principles Applied
- Martin Fowler's "Frequency Reduces Difficulty" — small changes done frequently
- "Pass the tests" first (Beck's rule #1) — all changes verified against existing 44 tests
- YAGNI — no new features, only behavior-preserving restructuring
- Command Query Separation — refactorings don't change observable behavior
