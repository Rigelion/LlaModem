# DB Progress: Dapper Migration

## Session Start
- User requested exploration of NuGet packages for cleaner SQL syntax
- Evaluated: Dapper, SqlKata, linq2db, raw SQL
- Decision: Dapper + SqlBuilder (smallest dependency, solves key pain points)
- Created: `db-plan.md`, `db-findings.md`, `db-progress.md`

## Current Phase
- **Phase 1: Add Dependencies** — not started

## Completed
- Phase 1: Add Dependencies ✅ (Dapper 2.1.72 + Dapper.SqlBuilder 2.1.66)
- Phase 2: Refactor GetDailyUsageAsync ✅ (SqlBuilder + QueryAsync)
- Phase 3: Refactor GetRecentRequestsAsync ✅ (SqlBuilder + QueryAsync)
- Phase 4: Clean Up ✅ (removed EnsureTableCreatedAsync, GetModelClause, AddModelParam)
- Build ✅ (0 warnings, 0 errors)
- Tests ✅ (52 passed)
- Smoke test ✅ (both endpoints return valid JSON)

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| `ToArray()` on `Task<IEnumerable<T>>` | 1 | Wrapped in parens: `(await conn.QueryAsync<T>(...)).ToArray()` |
| `??` on value type `UsageSummaryRow` | 1 | Used nullable T: `QueryFirstOrDefaultAsync<UsageSummaryRow?>` |
| Port 9000 in use during smoke test | 1 | Killed stale process with `lsof -ti:9000 | xargs kill -9` |
