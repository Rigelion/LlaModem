# Llama-server Usage Extraction — Findings & Fix

## Problem

Console warning: `WRN: Failed to extract token usage from response.`
Error: `JsonReaderException 'd' is invalid start of a value`

This occurs in `UsageCaptureMiddleware` when it tries to parse the upstream response body as JSON via `JsonDocument.Parse()`.

---

## Root Cause

The middleware's **streaming detection was broken**:

```csharp
// BEFORE (broken)
var isStreaming = context.Response.Headers.ContentType.ToString().Contains("stream", ...)
               || context.Response.Headers.TransferEncoding.Any();
```

These headers are checked **before** `_next(context)` runs — so they're always empty/default. The middleware incorrectly treats all responses as non-streaming, including SSE-formatted ones.

When llama-server sends an SSE response (`data: {...}\n\ndata: {...}`), the middleware buffers it and tries `JsonDocument.Parse()` on the raw text. The parser chokes on the `'d'` in `data:` → `JsonReaderException`.

---

## llama-server Response Format (confirmed via docs)

### Non-streaming (`stream: false` or omitted)
Standard OpenAI-compatible JSON:
```json
{
  "id": "chatcmpl-abc123",
  "object": "chat.completion",
  "created": 1677652288,
  "model": "...",
  "choices": [...],
  "usage": {
    "prompt_tokens": 28,
    "completion_tokens": 34,
    "total_tokens": 62
  },
  "timings": { ... }
}
```

### Streaming (`stream: true`)
SSE (Server-Sent Events) format:
```
data: {"id":"...","object":"chat.completion.chunk",...}

data: {"id":"...","choices":[{"delta":{"content":"..."}}]}

data: [DONE]
```

---

## Fix Applied

### 1. Detect streaming from the **request** body (where `stream: true` is sent)

```csharp
private static async Task<bool> IsStreamingRequestAsync(HttpRequest request)
{
    // Read request body, check for "stream": true, then reset position
    var doc = JsonDocument.Parse(body);
    return doc.RootElement.TryGetProperty("stream", out var sp) && sp.GetBoolean();
}
```

### 2. Handle SSE format in usage extraction

- `StripSseFormat()` — strips `data: ` prefix from each line
- `TryExtractUsageFromRaw()` — tries standard JSON first, falls back to NDJSON parsing
- `TryExtractModel()` — extracts model name from either format

### 3. Supported response shapes

| Format | Example | Handled? |
|--------|---------|----------|
| Standard OpenAI JSON | `{"usage":{"prompt_tokens":...}}` | ✅ |
| SSE (llama-server streaming) | `data: {...}\n\ndata: [DONE]` | ✅ |
| NDJSON (Ollama-style) | `{...}\n{...}` | ✅ |
| Empty body | `` | ✅ (no-op) |

---

## Files Changed

- `Middleware/UsageCaptureMiddleware.cs` — streaming detection + SSE/NDJSON handling

## Build Status

✅ Compiles clean. All 8 existing tests pass.

## Tests to Update

- `LlaModem.Tests/UsageCaptureMiddlewareTests.cs` — need tests for:
  - SSE response with usage in last chunk
  - NDJSON response with usage in last line
  - Request with `"stream": true` is not buffered

---

## Notes

- llama-server uses standard OpenAI field names: `prompt_tokens`, `completion_tokens`, `total_tokens`
- The `timings` object is llama-server specific (`prompt_ms`, `predicted_ms`, etc.) — not mapped to usage
- The fix preserves the original response body unchanged (pass-through)
