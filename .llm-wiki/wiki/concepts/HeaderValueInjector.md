---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-002]]
status: complete
---

# HeaderValueInjector

**Description:** Maps HTTP request headers to JSON body fields at root level. Auto-typed values (boolean, integer, double, string) based on header content.

## Configuration Pattern

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

## Injection Pattern

```csharp
public JsonElement Inject(JsonElement body, HttpRequestHeaders headers)
{
    if (!_enabled) return body;
    
    var objectNode = body.Deserialize<Dictionary<string, JsonElement>>() ?? new();
    
    foreach (var mapping in _mappings)
    {
        var headerName = mapping.Key.ToLowerInvariant();
        var headerValue = headers.FirstOrDefault(h => h.Key.ToLowerInvariant() == headerName).Value;
        
        if (headerValue.Any())
        {
            var jsonValue = ParseHeaderValue(headerValue.ToString());
            objectNode[mapping.Value] = jsonValue;
        }
    }
    
    return JsonDocument.Parse(JsonSerializer.Serialize(objectNode)).RootElement;
}
```

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

## Usage in Request Pipeline

Called by `ModelProxyHandler` before forwarding request to backend:
1. Read body from HttpContext
2. Parse JSON document
3. Inject headers into body at root level
4. Reset body stream position for forwarding
5. Forward modified request to backend

**Note:** Body modified before forwarding — original body lost unless captured by middleware.

## Related Entities

- [[entities/HeaderValueInjector]] - Header-to-body injection service
- [[entities/RequestForwarder]] - Proxy with header injection
- [[entities/ConfigRecords]] - Configuration records

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
