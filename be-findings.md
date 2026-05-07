# BE Findings: Stats Endpoint

## SQLite Schema (usage_records)

```sql
CREATE TABLE usage_records (
    id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp               TEXT    NOT NULL,
    request_id              TEXT,
    created                 TEXT,
    model                   TEXT    NOT NULL,
    route                   TEXT    NOT NULL,
    prompt_tokens           INTEGER NOT NULL DEFAULT 0,
    completion_tokens       INTEGER NOT NULL DEFAULT 0,
    total_tokens            INTEGER NOT NULL DEFAULT 0,
    prompt_ms               REAL,
    completion_ms           REAL,
    prompt_per_token_ms     REAL,
    completion_per_token_ms REAL,
    cache_hits              INTEGER,
    cached_tokens           INTEGER,
    request_time            TEXT,
    response_time           TEXT,
    client_ip               TEXT,
    status_code             INTEGER NOT NULL DEFAULT 200,
    request_headers         TEXT
);
CREATE INDEX idx_usage_records_timestamp ON usage_records(timestamp);
```

## Key Observations

1. **timestamp** is ISO 8601 TEXT — SQLite can do `date(timestamp)` for grouping
2. **cache_hits** is nullable INTEGER — cache hit rate = rows with cache_hits > 0 / total rows
3. **prompt_ms / completion_ms** are nullable REAL — avg should use `AVG()` which ignores NULLs
4. **model** is always present — easy filter/group dimension
5. **route** is always present — typically `/v1/chat/completions` or `/v1/completions`

## SQL Patterns Needed

### Daily aggregation
```sql
SELECT date(timestamp) as day,
       model,
       COUNT(*) as requests,
       SUM(prompt_tokens) as prompt_tokens,
       SUM(completion_tokens) as completion_tokens,
       SUM(total_tokens) as total_tokens,
       AVG(prompt_ms) as avg_prompt_ms,
       AVG(completion_ms) as avg_completion_ms,
       CAST(SUM(CASE WHEN cache_hits > 0 THEN 1 ELSE 0 END) AS REAL) / COUNT(*) as cache_hit_rate
FROM usage_records
WHERE timestamp >= date('now', ?)
  AND (? IS NULL OR model = ?)
GROUP BY day, model
ORDER BY day
```

### Paginated recent requests
```sql
SELECT id, timestamp, model, route,
       prompt_tokens, completion_tokens, total_tokens,
       prompt_ms, completion_ms, cache_hits,
       status_code, client_ip
FROM usage_records
WHERE (? IS NULL OR model = ?)
ORDER BY timestamp DESC
LIMIT ? OFFSET ?
```

### Total count (for pagination)
```sql
SELECT COUNT(*) FROM usage_records WHERE (? IS NULL OR model = ?)
```

## Design Decisions

- **No EF Core** — UsageDbContext uses raw SqliteCommand. Keep it that way for stats queries.
- **No new service** — add query methods directly to UsageDbContext. It's already a DB access layer.
- **Response models as records** — follow project style (F#-like, immutable).
- **No authorization** — `/admin/*` endpoints are currently unauthenticated. Keep it that way unless explicitly requested.
