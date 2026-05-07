# DB Plan: Migrate StatsService to Dapper + SqlBuilder

## Goal
Replace raw ADO.NET boilerplate in `StatsService` with Dapper for auto-mapping and `SqlBuilder` for dynamic WHERE clauses. Same API surface, cleaner code, less repetition.

## Current Pain Points
- Manual `while (reader.ReadAsync())` loops with ordinal-based column access
- `1 = 1 AND model = @model` SQL hack for optional filters
- Duplicated connection/command/reader lifecycle per query
- `EnsureTableCreatedAsync` mixed into query logic (should live elsewhere)
- `COALESCE`/`CASE WHEN COUNT(*) > 0` workarounds for empty aggregates

## Phases

### Phase 1: Add Dependencies
- [ ] `dotnet add package Dapper`
- [ ] `dotnet add package Dapper.SqlBuilder`
- **Status:** not_started

### Phase 2: Refactor GetDailyUsageAsync
- [ ] Use `SqlBuilder` with `AddTemplate("/**where**/")` for dynamic WHERE
- [ ] Replace `while (reader.ReadAsync())` with `conn.QueryAsync<DailyUsageRow>(sql, params)`
- [ ] Replace `ExecuteScalar` with `conn.ExecuteScalarAsync<int>(sql, params)`
- [ ] Remove `GetModelClause()` and `AddModelParam()` helpers
- **Status:** not_started

### Phase 3: Refactor GetRecentRequestsAsync
- [ ] Use `SqlBuilder` for count and data queries
- [ ] Replace manual reader loop with `QueryAsync<UsageRow>`
- [ ] Use `ExecuteScalarAsync<long>` for count
- **Status:** not_started

### Phase 4: Clean Up
- [ ] Remove `EnsureTableCreatedAsync()` — table creation belongs in `UsageDbContext.EnsureCreated()`
- [ ] Remove `GetModelClause()` and `AddModelParam()` static methods
- [ ] Remove `using` boilerplate — Dapper handles command lifecycle
- [ ] Simplify `Dispose()` (no-op remains fine)
- **Status:** not_started

### Phase 5: Build & Test
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all 52 pass
- [ ] Smoke test both endpoints
- **Status:** not_started

## Decision Log
| Decision | Rationale |
|----------|-----------|
| Keep `IStatsService` interface unchanged | No breaking changes for `EndpointSetup` |
| Keep `StatsResponse.cs` records unchanged | Dapper maps by column name, records have matching property names |
| Remove `EnsureTableCreatedAsync` | `UsageDbContext.EnsureCreated()` already creates the table; duplicate is a code smell |
| Use `QueryAsync<T>` not `Query<T>` | Async throughout, consistent with existing code |

## Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| Dapper maps by column name, not ordinal | Column aliases in SQL must match record property names (already done) |
| `SqlBuilder` template tokens look odd | Acceptable trade-off; cleaner than `1=1` hack |
| `EnsureTableCreatedAsync` removal breaks first-run | Verify `UsageDbContext.EnsureCreated()` is called at app startup (it is — confirmed in `Program.cs`) |
