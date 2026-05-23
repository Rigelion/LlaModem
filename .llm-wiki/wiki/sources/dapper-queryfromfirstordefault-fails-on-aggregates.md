---
type: source
title: "Dapper QueryFirstOrDefaultAsync fails silently on aggregate function return types"
slug: dapper-queryfromfirstordefault-fails-on-aggregates
status: insight
created: 2026-05-21
updated: 2026-05-21
category: bugfix
---
# Dapper QueryFirstOrDefaultAsync fails silently on aggregate function return types
## Problem
`conn.QueryFirstOrDefaultAsync<UsageSummaryRow?>(sql, params)` returned `null` even though the raw query executed correctly and returned a row. The same SQL executed via `QueryAsync` (dynamic) returned the expected row with correct data.

## Root Cause
Dapper's direct struct mapping (`QueryFirstOrDefaultAsync<T>`) failed to map SQLite aggregate function results to `UsageSummaryRow` record struct fields. The mismatch was in return types of `COALESCE`/`CASE WHEN` expressions — SQLite returns different numeric types than Dapper expected for the `long` and `double` fields. Unlike `QueryAsync` which uses dynamic typing, `QueryFirstOrDefaultAsync<T>` applies strict type mapping that silently fails for incompatible types.

## Fix
Replace `QueryFirstOrDefaultAsync<T>` with `QueryAsync` + manual conversion:
```csharp
var rows = (await conn.QueryAsync(sql, params)).ToList();
if (rows.Count == 0) return null;
var first = rows[0];
return new UsageSummaryRow(
    TotalRequests: Convert.ToInt64(first.TotalRequests),
    // ... etc
);
```

`Convert.ToInt64()` / `Convert.ToDouble()` handle the SQLite → C# numeric type conversion that Dapper's direct mapping couldn't.

## Lesson
When Dapper's strongly-typed query methods silently return default/null for queries known to return data, fall back to dynamic `QueryAsync` + manual mapping. This is especially likely with SQLite aggregate functions (`COUNT`, `SUM`, `AVG`) combined with `COALESCE`/`CASE WHEN` expressions that produce non-obvious numeric types.
*Category: bugfix*
---
*Captured: 2026-05-21*
## Related
_(Add [[wikilinks]] to related pages)_