---
type: source
title: "Nullability warning squelch strategy for test vs production code"
slug: nullable-warning-squelch-strategy
status: insight
created: 2026-05-23
updated: 2026-05-23
category: bugfix
---
# Nullability warning squelch strategy for test vs production code
## Strategy for nullability warnings (CS8602, CS8604, CS8605, CS8625)

When fixing C# nullable compiler warnings, distinguish between production and test code:

### Production code — fix the underlying issue
In `Services/OpenApiDocumentTransformer.cs`, used pattern matching with null check:
```csharp
// Before (CS8602 on document.Components.Schemas[name])
if (schema is not null && document.Components.Schemas is not null)
    document.Components.Schemas[name] = schema;

// After — uses pattern matching to avoid double-dereference
if (schema is not null && document.Components is { Schemas: { } schemas })
    schemas[name] = schema;
```

### Test code — suppress with `!` or file-level pragma
In test files, `JsonDocument` from `ReadFromJsonAsync<JsonDocument>()` returns nullable, and chained `.GetProperty()` calls trigger CS8602. For test fixtures where nullability is intentional:

1. Add `!` after `ReadFromJsonAsync<JsonDocument>()` to make the local non-nullable
2. Add `!` after `.RootElement` for each dereference
3. For files with many such warnings, add a file-level `#pragma warning disable CS8602`

Example from `OpenApiSchemaTests.cs`:
```csharp
#pragma warning disable CS8602 // Dereference of a possibly null reference — intentional in test fixtures

// ... all document.RootElement.GetProperty(...) calls are fine with the pragma
```

### Verification
After changes, `dotnet build 2>&1 | grep "warning"` should return empty.

*Category: bugfix*
---
*Captured: 2026-05-23*
## Related
_Add links to related pages._