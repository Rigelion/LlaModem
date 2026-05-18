# HeaderInjectionPattern

**Concept Type:** Architecture Pattern  
**Description:** Maps HTTP request headers to JSON body fields at root level with automatic type inference (boolean, integer, double, string).

## Purpose

Enable clients to pass generation parameters via HTTP headers instead of modifying request body. Useful for:
- Client libraries that wrap OpenAI SDK
- Proxy scenarios where headers are easier to manage
- Decoupling client configuration from API schema

## Configuration

Source: `RouterConfig.BodyHeaderMappings`:
```json
{
  "Router": {
    "EnableBodyHeaderInjection": true,
    "BodyHeaderMappings": {
      "x-client-id": "clientId",
      "x-debug": "debug",
      "x-priority": "priority"
    }
  }
}
```

**Behavior:**
- `EnableBodyHeaderInjection`: Toggle injection on/off (default: true)
- `BodyHeaderMappings`: Dictionary of header → JSON field name pairs
- Header names are case-insensitive (normalized to lowercase for lookup)

## Type Conversion Rules

| Header Value | Parsed As | Example |
|--------------|-----------|---------|
| `true`, `false` (case-insensitive) | Boolean | `X-Debug: TRUE` → `"debug": true` |
| `-123`, `0`, `456` (no decimals) | Integer | `X-Priority: -1` → `"priority": -1` |
| `1.23`, `0.5`, `-0.75` (with decimal) | Double | `X-Temp: 0.7` → `"temperature": 0.7` |
| Anything else | String | `X-Client-ID: abc123` → `"clientId": "abc123"` |

**Notes:**
- Parsing order: boolean → integer → double → string (most specific first)
- Culture-invariant parsing for doubles (uses `.` as decimal separator)
- Empty header values skipped (no null/empty fields injected)

## Injection Flow

1. Client request with headers (`X-Llama-Temperature: 0.7`)
2. `HeaderValueInjector.Inject()` called by `ModelProxyHandler`
3. Read body from HttpContext
4. Parse JSON document
5. For each mapping, check if header exists and has value
6. Parse value to appropriate type (bool/int/double/string)
7. Inject at JSON root level
8. Reset body stream position for forwarding
9. Forward modified request to backend

## Example Transformation

**Request:**
```http
POST /v1/chat/completions
X-Llama-Model: qwen36-smart
X-Llama-Temperature: 0.7
X-Llama-TopP: 0.95
Content-Type: application/json

{
  "model": "qwen36-smart",
  "messages": [{"role": "user", "content": "Hello"}]
}
```

**After Injection:**
```json
{
  "model": "qwen36-smart",
  "messages": [{"role": "user", "content": "Hello"}],
  "temperature": 0.7,
  "top_p": 0.95
}
```

## Implementation Details

**Location:** `HeaderValueInjector` service (see [[HeaderValueInjector]])

**Case-insensitive matching:** Header names normalized to lowercase (`x-llama-model` → `x-llama-model`).

**Null-safe:** Returns original body if injection disabled or no matching headers.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Header not in mappings | Skipped (no injection) |
| Empty header value | Skipped (no null/empty fields) |
| Invalid JSON body | Exception thrown (propagated to caller) |
| Type parse failure | Falls back to string |

## Security Notes

- **Header values trusted:** No validation on header content (assumes controlled environment)
- **Body modification:** Injected fields appear at JSON root — may conflict with existing fields
- **Case sensitivity:** Header names normalized to lowercase for matching
- **Enable/disable:** Toggle via config (`EnableBodyHeaderInjection: true`)

## Related Entities

- [[HeaderValueInjector]]
- [[RequestForwarder]]
- [[RouterConfig]]