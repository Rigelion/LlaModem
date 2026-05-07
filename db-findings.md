# DB Findings: Dapper + SqlBuilder Research

## Package Details

### Dapper
- **NuGet:** `Dapper` (latest stable)
- **Size:** ~50KB — micro-ORM, zero dependencies beyond `System.Runtime.CompilerServices.Unsafe`
- **What it does:** Extension methods on `IDbConnection` that auto-map `SqlDataReader` results to POCOs/records
- **Key methods:**
  - `conn.QueryAsync<T>(sql, param)` → `T[]` — maps reader to typed results
  - `conn.ExecuteScalarAsync<T>(sql, param)` → `T?` — single value
  - `conn.QueryFirstOrDefaultAsync<T>(sql, param)` → `T?` — first row or default
- **Mapping:** By column name (via `DbDataReader.GetData()` ordinal lookup), case-insensitive
- **Async:** All methods have `Async` variants; supports `CancellationToken`

### Dapper.SqlBuilder
- **NuGet:** `Dapper.SqlBuilder` (separate package, included in Dapper 1.6+ ecosystem)
- **What it does:** Dynamic SQL composition with `/**placeholder**/` tokens
- **Key API:**
  - `builder.AddTemplate("/**where**/")` — reserves a token
  - `builder.Where("col = @val", new { val })` — appends `AND col = @val`
  - `builder.OrderBy("col")` — appends `ORDER BY col`
  - `builder.GroupBy("col")` — appends `GROUP BY col`
  - `conn.QueryAsync<T>(template.Sql, template.Param)` — resolves and executes
- **Joiners:** `Where` joins with `AND`, `OrWhere` joins with `OR`
- **Prefix/Postfix:** `Where` adds `WHERE ` prefix on first clause, subsequent clauses get `AND `

## How It Replaces Current Code

### Before: Manual Reader Loop
```csharp
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = sql;
    cmd.Parameters.AddWithValue("@cutoff", cutoff);
    AddModelParam(cmd, model);

    await using var reader = await cmd.ExecuteReaderAsync();
    var rows = new List<DailyUsageRow>();
    while (await reader.ReadAsync())
    {
        rows.Add(new DailyUsageRow(
            Date: reader.GetString(0),
            Model: reader.GetString(1),
            Requests: reader.GetInt32(2),
            // ... 6 more ordinal-based reads
        ));
    }
    daily = [.. rows];
}
```

### After: Dapper QueryAsync
```csharp
var daily = await conn.QueryAsync<DailyUsageRow>(sql, new { cutoff, model });
```

### Before: 1=1 Hack
```sql
WHERE timestamp >= @cutoff
  {GetModelClause(model)}   -- "AND 1 = 1" or "AND model = @model"
```

### After: SqlBuilder
```csharp
var builder = new SqlBuilder();
var where = builder.AddTemplate("/**where**/");
builder.Where("timestamp >= @cutoff", new { cutoff });
if (model != null)
    builder.Where("model = @model", new { model });

var sql = $"""
    SELECT date(timestamp) as day, model, COUNT(*) as requests, ...
    FROM usage_records
    /**where**/
    GROUP BY day, model
    ORDER BY day
    """;

var daily = await conn.QueryAsync<DailyUsageRow>(sql, new { cutoff, model });
```

## Column Name Mapping

Dapper matches by column alias name. Current SQL already uses aliases matching record properties:

| Column Alias | Record Property | Match? |
|---|---|---|
| `date` | `Date` | ✅ (case-insensitive) |
| `model` | `Model` | ✅ |
| `requests` | `Requests` | ✅ |
| `prompt_tokens` | `PromptTokens` | ✅ |
| `completion_tokens` | `CompletionTokens` | ✅ |
| `total_tokens` | `TotalTokens` | ✅ |
| `avg_prompt_ms` | `AvgPromptMs` | ✅ |
| `avg_completion_ms` | `AvgCompletionMs` | ✅ |
| `cache_hit_rate` | `CacheHitRate` | ✅ |

No changes needed to `StatsResponse.cs`.

## What Gets Removed

| Current Code | Replacement |
|---|---|
| `GetModelClause(string? model)` → `"AND 1 = 1"` / `"AND model = @model"` | `SqlBuilder.Where()` |
| `AddModelParam(SqliteCommand cmd, string? model)` | `SqlBuilder.Where("model = @model", new { model })` |
| `while (reader.ReadAsync())` + ordinal reads | `conn.QueryAsync<T>(sql, param)` |
| `Convert.ToInt64(await cmd.ExecuteScalarAsync()!)` | `await conn.ExecuteScalarAsync<long>(sql, param)` |
| `EnsureTableCreatedAsync()` in StatsService | Move to `UsageDbContext` or app startup |
| `using (var cmd = conn.CreateCommand())` boilerplate | Dapper creates/disposes commands internally |

## SqlBuilder Template Syntax

The `/**token**/` convention is the only "magic" in SqlBuilder:

```csharp
var builder = new SqlBuilder();
var where = builder.AddTemplate("/**where**/");
var orderby = builder.AddTemplate("/**orderby**/");

builder.Where("timestamp >= @cutoff", new { cutoff });
if (model != null) builder.Where("model = @model", new { model });
builder.OrderBy("day");

var sql = $"""
    SELECT ... FROM usage_records
    /**where**/
    GROUP BY day
    /**orderby**/
    """;

// Resolved SQL:
// SELECT ... FROM usage_records
// WHERE timestamp >= @cutoff AND model = @model
// GROUP BY day
// ORDER BY day
```

If no `Where` clauses are added, `/**where**/` is replaced with nothing (not `WHERE` with no condition).
