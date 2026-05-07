# FE Findings: Stats Dashboard

## Current LlaModem Endpoints (for reference)

| Path | Method | Auth | Description |
|------|--------|------|-------------|
| `/health` | GET | — | Health check |
| `/v1/{**path}` | POST | Basic Auth | OpenAI-compatible proxy |
| `/admin/status` | GET | — | Active model + backend URL |
| `/admin/model` | POST | — | Switch model |
| `/admin/stop` | POST | — | Stop active model |

## New Endpoints (BE track)

| Path | Method | Auth | Description |
|------|--------|------|-------------|
| `/admin/stats/usage` | GET | — | Daily aggregated usage |
| `/admin/stats/requests` | GET | — | Paginated recent requests |

## CORS Consideration

Since FE and BE will be separate projects:
- **Option A**: Vite dev proxy (`server.proxy` in vite.config.ts) → no CORS needed during dev
- **Option B**: Production build served from BE → no CORS needed
- **Option C**: Cross-origin → need `CORS` middleware on BE

**Recommendation**: Option A for dev, Option B for production. Both avoid CORS entirely.

## Vite Proxy Config (Option A)

```ts
// vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/admin': {
        target: 'http://localhost:9000',
        changeOrigin: true,
      }
    }
  }
});
```

## Data Volume Considerations

- SQLite is single-file, no connection pooling issues
- Daily aggregation runs on the DB side — efficient
- Recent requests uses LIMIT/OFFSET — fine for reasonable limits
- No pagination cursor needed (simple offset pagination is sufficient)

## TypeScript Considerations

- Use `fetch` API — no need for axios in a stats dashboard
- All timestamps are ISO 8601 strings — parse with `new Date()` or `Intl.DateTimeFormat`
- Nullable fields (`promptMs`, `cacheHits`, `clientIp`) — handle with `??` or optional chaining
- Numbers are `number | null` where SQLite has nullable columns
