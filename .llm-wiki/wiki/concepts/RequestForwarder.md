---
type: concept
created: 2026-05-18
updated: 2026-05-19
sources:
  - [[sources/SRC-2026-05-18-001]]
  - [[sources/SRC-2026-05-18-002]]
  - [[sources/SRC-2026-05-18-003]]
status: complete
---

# RequestForwarder

**Description:** Proxies HTTP requests from clients to llama-server backends. Handles header injection, backend URL selection, and error response formatting.

## Proxy Pattern

1. Client request (`/v1/chat/completions`)
2. `HeaderValueInjector` injects `X-Llama-*` headers to JSON body
3. `ModelProxyHandler` selects backend URL, prepares HttpClient request
4. `RequestForwarder` proxies to backend, captures response
5. `ResponseUsageMiddleware` captures usage stats from response

## Header Injection Pattern
Configuration: `RouterConfig.BodyHeaderMappings` (e.g., `"x-client-id": "clientId"`). Values auto-typed: boolean, integer, double, or string. Example: `X-Llama-Temperature: 0.7` → body `{ "temperature": 0.7, ... }`.

## Backend URL Selection
Source: `ModelConfig.BackendUrl` (per-model) or `AppConfig.BackendUrl` (default). Default is `http://localhost:8001`. All models share port 8001 (sequential execution).

## Error Handling Pattern
Structured errors via `ErrorResponseWriter.WriteAsync()` with consistent JSON format:
```json
{ "error": { "code": "BACKEND_UNAVAILABLE", "message": "llama-server backend not responding" } }
```
Error codes: `MODEL_NOT_FOUND`, `BACKEND_UNAVAILABLE`, `INVALID_REQUEST`, `INTERNAL_ERROR`.

## Critical Notes

- **No streaming:** Full response buffered for usage capture (streaming not supported)
- **Timeouts:** 5-minute request timeout via `IHttpClientFactory`
- **Header injection:** Only applies to `/v1/*` proxy routes

## Related Entities

- [[entities/RequestForwarder]] - HTTP proxy service
- [[entities/HeaderValueInjector]] - Header-to-body injection service
- [[entities/ResponseUsageMiddleware]] - Usage capture middleware
- [[entities/ConfigRecords]] - Configuration records

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
