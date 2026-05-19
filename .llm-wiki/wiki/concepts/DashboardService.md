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

# DashboardService

**Description:** Admin endpoint handlers for model control, status queries, and usage statistics. All endpoints are unauthenticated (open to local network).

## Admin Endpoints

### GET /health
Router health check + active model name. Returns healthy status and current active model.

### GET /admin/status  
Current model state and backend URL. Returns null fields if no model active.

### POST /admin/model
Switch to specified model. Validates model exists, calls `ModelManager.EnsureModelAsync()` with optional launch params.

### POST /admin/stop
Stop the currently active model. Clears state repository on success.

### GET /admin/stats/usage
Daily aggregated token usage. Query params: `days` (default 30), `model` (filter). Returns daily usage array with token counts and request count.

### GET /admin/stats/requests
Paginated recent requests. Query params: `limit` (default 50, max 100), `offset`, `model`. Includes timing data if available.

### GET /admin/stats/cost-comparison
Estimate cloud model costs for same token usage. Compares against local model usage using `ModelPricing` table. Lists 6 major cloud models (Claude, GPT, Gemini, Qwen).

## Endpoint Registration Pattern
Endpoints registered via extension method: `app.MapStatsEndpoints()` in `Program.cs`. Uses Minimal API with `[HttpGet]` / `[HttpPost]` attributes.

## Security Notes
All admin endpoints unauthenticated — accessible to anyone on local network. Intended for local development/trusted environment only. Proxy routes (`/v1/*`) require Basic Auth.

## Related Entities

- [[entities/DashboardService]] - Admin endpoint handlers
- [[entities/ModelManager]] - Model lifecycle orchestration
- [[entities/StatsService]] - Usage aggregation service
- [[entities/UsageService]] - Token usage persistence

## Related Synthesis

- [[concepts/LlaModemArchitectureOverview]] - Architecture overview tying all components together
