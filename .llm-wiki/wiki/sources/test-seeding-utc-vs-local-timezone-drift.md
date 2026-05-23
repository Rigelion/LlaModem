---
type: source
title: "Test seeding uses UTC but assertions used local time causing date drift"
slug: test-seeding-utc-vs-local-timezone-drift
status: insight
created: 2026-05-21
updated: 2026-05-21
category: bugfix
---
# Test seeding uses UTC but assertions used local time causing date drift
## Problem
Tests seeded data using `DateTimeOffset.UtcNow.AddDays(-N)` for timestamps, but assertions compared against `DateTime.Now.Date.AddDays(-N)`. Due to timezone offset (UTC vs local), the dates could differ by 1 day when local time is behind UTC (e.g., UTC is May 21 AM but local is May 20 PM).

## Fix
Capture `DateTimeOffset.UtcNow` once at test start and use it for ALL date assertions. Also verify that seeded timestamps match the service's internal cutoff logic (which uses `DateTimeOffset.UtcNow.AddDays(-days)`).

## Lesson
When tests seed time-based data, both seeding AND assertions must use the same timezone reference point. Capture `DateTimeOffset.UtcNow` once at test start and reuse it throughout to avoid drift between seeding and assertion.
*Category: bugfix*
---
*Captured: 2026-05-21*
## Related
_(Add [[wikilinks]] to related pages)_