# StatsServiceTests Failure Analysis

## Issue Summary
Multiple `StatsServiceTests` tests are failing, specifically those using model filters:
- `GetDailyUsageAsync_ModelFilter_ReturnsOnlyMatchingModel` - expects 2 rows, gets 0
- `GetRecentRequestsAsync_ModelFilter_ReturnsOnlyMatchingModel` - similar issue pattern

## Investigation Results

### SQL Query Works Correctly in Isolation
Tested the exact query from `SqliteUsagePersistence.GetDailyRowsAsync()` with model filter:
```sql
SELECT DATE(timestamp), COUNT(*) 
FROM usage_records 
WHERE timestamp >= @cutoff AND model = @model 
GROUP BY DATE(timestamp), model
```

**Result**: Returns 2 rows for llama3.2 as expected when run directly against SQLite.

### Timestamp Parsing Works Correctly
- UTC timestamps with "Z" suffix parse correctly: `DateTimeOffset.Parse("2026-05-18T08:00:00.0000000Z")`
- String comparison in SQLite works for ISO 8601 format
- Cutoff calculation (`DateTimeOffset.UtcNow.AddDays(-30)`) correctly includes seeded data

### Database Persistence Works Correctly
- Data written to one connection is visible from fresh connections using same file path
- No issues with SQLite journaling or transaction isolation
- File size and contents remain consistent across connection boundaries

## Root Cause Hypothesis

The issue appears to be related to **separate persistence instances** in tests:

```csharp
// SeedDataAsync creates its own persistence instance
var persistence = new SqliteUsagePersistence(...);
await persistence.AppendAsync(...);

// CreateService() creates a NEW service with NEW persistence instance
var service = CreateService(); // Creates fresh SqliteUsagePersistence
var result = await service.GetDailyUsageAsync(30, "llama3.2");
```

While both use the same `_tempDb` path, potential issues:
1. **Connection timing**: Seeding connection may not have fully flushed before query
2. **SQLite WAL mode**: Write-ahead logging could cause visibility delays
3. **File handle contention**: Multiple connections to same file simultaneously

## Test Structure Issue

```csharp
[Fact]
public async Task GetDailyUsageAsync_ModelFilter_ReturnsOnlyMatchingModel()
{
    await SeedDataAsync(...);  // Creates persistence A, seeds data
    var service = CreateService(); // Creates service with persistence B (fresh)
    var result = await service.GetDailyUsageAsync(30, "llama3.2");
    // Result: 0 rows instead of expected 2
}
```

## Recommended Fixes

1. **Use single persistence instance**: Pass same `SqliteUsagePersistence` to both seed and query
2. **Explicit connection disposal**: Add `await conn.CloseAsync()` after seeding
3. **Verify file path**: Log actual `_tempDb` path in tests to confirm consistency
4. **Add SQLite pragma**: Set `PRAGMA journal_mode = DELETE;` for immediate visibility

## Files Involved
- `LlaModem.Tests/StatsServiceTests.cs` - Test implementation
- `Services/SqliteUsagePersistence.cs` - Persistence layer with SQL queries
- `Services/StatsService.cs` - Service wrapping persistence

## Status
Analysis complete. Ready to implement fixes when needed.
