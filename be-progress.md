# BE Progress: Stats Endpoint

## Session Start
- Old planning files deleted (task_plan.md, findings.md, progress.md)
- New BE track started: be-plan.md, be-findings.md, be-progress.md

## Current Phase
- **Phase 4: Build & Test** — ✅ Complete
- **Phase 5: Pending** — (none yet)

## Completed
- Phase 1: Query layer (`StatsService` with daily aggregation + pagination) ✅
- Phase 2: Response models (`DailyUsageRow`, `UsageRow`, `UsageSummaryRow`, DTOs) ✅
- Phase 3: Endpoint registration (`/admin/stats/usage`, `/admin/stats/requests`) ✅
- Phase 4: Build ✅ (0 warnings, 0 errors) + Tests ✅ (52 passed) + Smoke test ✅ (both endpoints return valid JSON)
- **Follow-up planned:** Dapper + SqlBuilder migration (see `db-plan.md`)

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| `SqliteException: near "1": syntax error` | 1 | Added `AND` prefix to model clause (`AND 1 = 1`) |
| `SqliteException: table usage_records does not exist` | 1 | Added `EnsureTableCreatedAsync()` auto-creates table/index |
| `InvalidOperationException: Must add values for parameters` | 1 | Switched from `?` to named params (`@cutoff`, `@model`, `@limit`, `@offset`) |
| `InvalidOperationException: The data is NULL at ordinal 1` (summary) | 1 | Wrapped `SUM()`/`AVG()` in `COALESCE(..., 0)` |

