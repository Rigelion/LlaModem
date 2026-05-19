# ModelProxyHandler

**Interface**: `IModelProxyHandler`  
**Location**: `Services/ModelProxyHandler.cs`

Routes incoming requests to appropriate llama-server backends based on `X-Llama-Model` header, manages model lifecycle (lazy start, hot switching), and handles parameter loading from `dashboard_params.json`.

## Key Responsibilities

1. Extract model name from `X-Llama-Model` header
2. Validate model exists in configuration
3. Load launch parameters from file or use defaults
4. Trigger model startup via `IMetaModelManager.EnsureModelAsync()`
5. Forward request to backend after health check passes

## Parameter Loading Strategy

**Source**: `dashboard_params.json` (persisted via admin API)  
**Fallback**: `ModelLaunchParams.Defaults` when file missing or model not found

Changed from `double?` to `decimal?` for exact 2-decimal precision compatibility with llama-server.

## Related Components

- [[Services/DashboardService]] — Parameter persistence and loading
- [[Services/ModelManager]] — Model lifecycle operations  
- [[Config/architecture]] — AppConfig and model configuration structure