---
type: entity
created: 2026-05-18
updated: 2026-05-19
status: complete
---

# HeaderValueInjector

**Entity Type:** Service  
**Responsibility:** Maps HTTP request headers to JSON body fields at root level. Auto-typed values (boolean, integer, double, string) based on header content.

## Dependencies

- `IOptions<RouterConfig>` - Header mapping configuration (`BodyHeaderMappings`)
- `ILogger<HeaderValueInjector>` - Injection logging

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

private static JsonElement ParseHeaderValue(string value)
{
    // Try boolean
    if (bool.TryParse(value, out var @bool))
        return JsonElement.Create(@bool);
    
    // Try integer
    if (int.TryParse(value, out var @int))
        return JsonElement.Create(@int);
    
    // Try double
    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var @double))
        return JsonElement.Create(@double);
    
    // Default: string
    return JsonElement.Create(value);
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

## Header Name Normalization

Case-insensitive matching: `X-Llama-Model` → `x-llama-model` (lowercase). Implemented via `_mappings.Keys.ToLowerInvariant()` for lookup.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Header not in mappings | Skipped (no injection) |
| Empty header value | Skipped (no null/empty fields) |
| Invalid JSON body | Exception thrown (propagated to caller) |
| Type parse failure | Falls back to string |

## Critical Notes

- **Root-level only:** Fields injected at JSON root, not nested
- **Type inference:** Auto-typed based on value content — no explicit type declarations
- **Empty values skipped:** Prevents null/empty fields in request body
- **Disabled by default:** Must enable via config (`EnableBodyHeaderInjection: true`)

## Related Entities

- [[entities/RequestForwarder]] - Uses injector before proxying requests
- [[entities/ConfigRecords]] - Router configuration (mappings, toggle)

## Related Concepts

- [[entities/HeaderValueInjector]] - Header-to-body injection with type inference