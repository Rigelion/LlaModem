# Usage Tracking — LlaModem

## Overview

LlaModem captures token usage statistics from all proxied LLM API responses and persists them to daily markdown files. The middleware intercepts non-streaming responses, extracts `usage` and `timings` data from the JSON body, and writes a formatted report to disk.

## Configuration

Add a `Usage` section to `appsettings.json`:

```json
{
  "Usage": {
    "Enabled": true,
    "Path": "usage",
    "FilenamePattern": "usage-{date}.md",
    "IncludeTimings": true
  }
}
```

| Property           | Type    | Default     | Description                                           |
|--------------------|---------|-------------|-------------------------------------------------------|
| `Enabled`          | `bool`  | `true`      | Whether to capture and persist token usage stats.     |
| `Path`             | `string`| `"usage"`   | Directory for daily markdown files (relative to app base dir). |
| `FilenamePattern`  | `string`| `"usage-{date}.md"` | Pattern with `{date}` replaced by `yyyy-MM-dd`. |
| `IncludeTimings`   | `bool`  | `true`      | Whether to include timing data in reports.            |

## How It Works

### Capture Pipeline

```
llama-server response (JSON)
  → UsageCaptureMiddleware intercepts non-streaming responses
  → Extracts usage + timings from JSON
  → UsageService writes to daily markdown file
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

## Output Format

Daily markdown files are created in the configured `Path` directory:

```markdown
# Usage Report — 2026-05-07

| Timestamp | Model | Route | Prompt Tokens | Completion Tokens | Total Tokens | Prompt Ms | Completion Ms | Cache Hits |
|-----------|-------|-------|---------------|-------------------|--------------|-----------|---------------|------------|
| 2026-05-07 10:00:00 | llama3.2 | /v1/chat/completions | 10 | 50 | 60 | 120.5 | 450.3 | 15 |
| 2026-05-07 10:05:00 | mistral | /v1/chat/completions | 20 | 100 | 120 | 200.0 | 800.0 | 8 |
```

When `IncludeTimings` is `false`, the timing columns are omitted entirely from the header and data rows.

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
    DateTime Timestamp,
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
                          │   (markdown file) │
                          └──────────────────┘
```

### Key Components

| Component | File | Responsibility |
|-----------|------|----------------|
| `UsageCaptureMiddleware` | `Middleware/UsageCaptureMiddleware.cs` | Intercepts responses, extracts usage + timings |
| `UsageService` | `Services/UsageService.cs` | Persists to daily markdown files |
| `UsageConfig` | `Config/UsageConfig.cs` | Configuration options |
| `TokenUsage` | `Models/TokenUsage.cs` | Token count data model |
| `Timings` | `Models/Timings.cs` | Timing data model |
| `SessionEntry` | `Models/SessionEntry.cs` | Per-request session entry |

## Logging

The middleware logs each captured request:

```
[USAGE] llama3.2 /v1/chat/completions — Prompt: 10, Completion: 50, Total: 60, Prompt: 120.5ms, Completion: 450.3ms
```

Timing info is included in logs only when timing data is present.

## Testing

Unit tests cover:
- Standard JSON response extraction
- NDJSON parsing with timings
- Missing timings (graceful handling)
- Partial timings (some fields present, others null)
- Markdown output with/without timing columns
- Empty responses and missing usage fields

Run tests:
```bash
dotnet test
```
