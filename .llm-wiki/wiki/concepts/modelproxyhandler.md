# ModelProxyHandler Concept

**Interface**: `IModelProxyHandler`  
**Role**: Request routing and model lifecycle coordination

## Key Concepts

1. **Header-based routing**: Extracts model name from `X-Llama-Model` header
2. **Parameter loading**: Loads launch params from `dashboard_params.json` (file-based, not HTTP headers)
3. **Lazy start**: Models only launch when first requested
4. **Hot switching**: Requesting different model stops current, starts new
5. **Health check polling**: Verifies backend ready before forwarding

## Parameter Type Precision

**Changed from `double?` to `decimal?`** for exact 2-decimal llama-server compatibility.

## Related Concepts

- [[ConfigRecords]] — Configuration binding pattern
- [[DashboardService]] — Parameter persistence and loading
- [[ModelManager]] — Model lifecycle operations