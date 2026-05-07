# Usage Tracking — LlaModem

## Overview

LlaModem captures token usage statistics from all proxied LLM API responses and persists them to a SQLite database. The middleware intercepts non-streaming responses, extracts `usage` and `timings` data from the JSON body, and writes structured records to disk.

## Configuration

Add a `Usage` section to `appsettings.json`:

```json
{
  "Usage": {
    "Enabled": true,
    "Path": "usage/usage.db",
    "IncludeTimings": true
  }
}
```

| Property           | Type    | Default           | Description                                           |
|--------------------|---------|-------------------|-------------------------------------------------------|
| `Enabled`          | `bool`  | `true`            | Whether to capture and persist token usage stats.     |
| `Path`             | `string`| `"usage/usage.db"`| Path to the SQLite database file (relative to app base dir). |
| `IncludeTimings`   | `bool`  | `true`            | Whether to include timing data in records.            |

## How It Works

### Capture Pipeline

```
llama-server response (JSON)
  → UsageCaptureMiddleware intercepts non-streaming responses
  → Extracts usage + timings from JSON
  → UsageService writes to SQLite database
  → Original response passes through unchanged
```

### Extracted Data

#### Token Usage

| Field              | Source              | Description              |
|--------------------|---------------------|--------------------------|
| `PromptTokens`     | `usage.prompt_tokens`     | Tokens in the prompt             |
| `CompletionTokens` | `usage.completion_tokens` | Tokens generated             |
| `TotalTokens`      | `usage.total_tokens`      | Total tokens (prompt + completion) |

#### Timings (when `IncludeTimings: true`)

| Field              | Source (llama-server)      | Description              |
|--------------------|----------------------------|--------------------------|
| `PromptMs`         | `timings.prompt_ms`        | Total prefill time (ms)  |
| `CompletionMs`     | `timings.predicted_ms`     | Total decode time (ms)   |
| `PromptPerTokenMs` | `timings.prompt_per_token_ms` | Per-token prefill time |
| `CompletionPerTokenMs` | `timings.predicted_per_token_ms` | Per-token decode time |
| `CacheHits`        | `timings.cache_n`          | KV cache hit count       |

> **Note:** `predicted_ms` is mapped to `CompletionMs` — llama-server calls it "predicted" but "completion" is the more common terminology.

### Supported Response Formats

The middleware handles:
- **Standard JSON** — single response with `usage` and `timings` at the root level
- **NDJSON** — multiple JSON objects, one per line (last one with `usage` is used)
- **SSE** — `data: {...}` prefixed lines (prefixed lines are stripped before parsing)

### Affected Routes

| Route Pattern              | Captured |
|----------------------------|----------|
| `/v1/chat/completions`     | ✅       |
| `/v1/completions`          | ✅       |
| All other routes           | ❌       |

## Database Schema

The SQLite database contains a single table:

```sql
CREATE TABLE usage_records (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp             TEXT    NOT NULL,
    model                 TEXT    NOT NULL,
    route                 TEXT    NOT NULL,
    prompt_tokens         INTEGER NOT NULL,
    completion_tokens     INTEGER NOT NULL,
    total_tokens          INTEGER NOT NULL,
    prompt_ms             REAL,
    completion_ms         REAL,
    prompt_per_token_ms   REAL,
    completion_per_token_ms REAL,
    cache_hits            INTEGER,
    INDEX idx_usage_records_timestamp (timestamp)
);
```

### Querying Usage Data

```bash
# All records
sqlite3 usage/usage.db "SELECT * FROM usage_records ORDER BY timestamp DESC;"

# Token totals
sqlite3 usage/usage.db "SELECT model, SUM(prompt_tokens) as prompt, SUM(completion_tokens) as completion, SUM(total_tokens) as total FROM usage_records GROUP BY model;"

# Daily totals
sqlite3 usage/usage.db "SELECT DATE(timestamp) as day, COUNT(*) as requests, SUM(total_tokens) as tokens FROM usage_records GROUP BY day ORDER BY day;"

# With timings
sqlite3 usage/usage.db "SELECT timestamp, model, route, prompt_tokens, completion_tokens, prompt_ms, completion_ms, cache_hits FROM usage_records ORDER BY timestamp DESC;"
```

## Data Model

### `TokenUsage` Record

```csharp
public record TokenUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? PromptMs = null,
    double? CompletionMs = null,
    double? PromptPerTokenMs = null,
    double? CompletionPerTokenMs = null,
    int? CacheHits = null)
{
    public Timings? Timings => (PromptMs.HasValue || CompletionMs.HasValue ||
        PromptPerTokenMs.HasValue || CompletionPerTokenMs.HasValue || CacheHits.HasValue)
        ? new(PromptMs, CompletionMs, PromptPerTokenMs, CompletionPerTokenMs, CacheHits)
        : null;
}
```

### `Timings` Record Struct

```csharp
public readonly record struct Timings(
    double? PromptMs,
    double? CompletionMs,
    double? PromptPerTokenMs,
    double? CompletionPerTokenMs,
    int? CacheHits);
```

### `SessionEntry` Record

```csharp
public record SessionEntry(
    DateTimeOffset Timestamp,
    string Model,
    string Route,
    TokenUsage Usage)
{
    public Timings? Timings => Usage.Timings;
}
```

## Architecture

```
┌─────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│  llama-server   │────▶│ UsageCaptureMiddleware│────▶│  Original       │
│  (JSON response)│     │  (extract usage+timings)│   │  Response       │
└─────────────────┘     └──────────┬───────────┘     └─────────────────┘
                                   │
                                   ▼
                          ┌──────────────────┐
                          │   UsageService    │
                          │   (SQLite insert) │
                          └──────────────────┘
```

### Key Components

| Component | File | Responsibility |
|-----------|------|----------------|
| `ResponseUsageMiddleware` | `Middleware/ResponseUsageMiddleware.cs` | Intercepts responses, extracts usage + timings |
| `UsageService` | `Services/UsageService.cs` | Persists to SQLite database |
| `UsageDbContext` | `Services/UsageDbContext.cs` | Raw SQLite connection and schema management |
| `UsageConfig` | `Config/UsageConfig.cs` | Configuration options |
| `TokenUsage` | `Models/TokenUsage.cs` | Token count data model |
| `Timings` | `Models/TokenUsage.cs` | Timing data model |
| `SessionEntry` | `Models/SessionEntry.cs` | Per-request session entry |

## Logging

The middleware logs each captured request:

```
[USAGE] llama3.2 /v1/chat/completions — Prompt: 10, Completion: 50, Total: 60, Prompt: 120.5ms, Completion: 450.3ms
```

Timing info is included in logs only when timing data is present.

## Testing

Unit tests cover:
- Database creation and record insertion
- Multiple record append
- Timestamp storage and retrieval
- Timing data with and without `IncludeTimings`
- Null timing handling
- Auto-creation of database directories

Run tests:
```bash
dotnet test
```
