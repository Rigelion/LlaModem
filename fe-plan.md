# FE Plan: Stats Dashboard Contract

## Goal
Define the API contract and TypeScript types for a Vite + TypeScript frontend that consumes LlaModem's stats endpoints.

## Architecture Decision

- **Separate project** — not embedded in LlaModem
- **TypeScript + Vite** — lightweight, fast dev server
- **Framework** — TBD (SvelteKit / Vue / React — contract is framework-agnostic)
- **Location**: `fe-stats/` alongside `llamodem/` in the same workspace

## API Contract (TypeScript Types)

### Daily Usage

```typescript
interface DailyUsageResponse {
  period: {
    from: string;  // ISO 8601
    to: string;    // ISO 8601
  };
  summary: {
    totalRequests: number;
    totalPromptTokens: number;
    totalCompletionTokens: number;
    totalTokens: number;
    avgPromptMs: number;
    avgCompletionMs: number;
    cacheHitRate: number;  // 0..1
  };
  daily: DailyUsageRow[];
}

interface DailyUsageRow {
  date: string;            // "YYYY-MM-DD"
  requests: number;
  model: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  avgPromptMs: number;
  avgCompletionMs: number;
  cacheHitRate: number;    // 0..1
}
```

### Recent Requests

```typescript
interface RecentRequestsResponse {
  total: number;
  offset: number;
  limit: number;
  items: UsageRow[];
}

interface UsageRow {
  id: number;
  timestamp: string;       // ISO 8601
  model: string;
  route: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  promptMs: number | null;
  completionMs: number | null;
  cacheHits: number | null;
  statusCode: number;
  clientIp: string | null;
}
```

### API Client

```typescript
interface StatsApi {
  /** GET /admin/stats/usage?days=30&model=qwen-smart */
  getDailyUsage(days?: number, model?: string): Promise<DailyUsageResponse>;

  /** GET /admin/stats/requests?model=qwen-smart&limit=50&offset=0 */
  getRecentRequests(options?: {
    model?: string;
    limit?: number;
    offset?: number;
  }): Promise<RecentRequestsResponse>;
}
```

## Phases

### Phase 1: Project scaffold
- [ ] `fe-stats/` project — `npm create vite@latest fe-stats -- --template ts`
- [ ] Configure Vite proxy to `http://localhost:9000`
- [ ] Set up `src/api/` with fetch wrapper
- [ ] Define all TypeScript types in `src/types/stats.ts`
- **Status:** not started

### Phase 2: API client
- [ ] `src/api/statsApi.ts` — implements `StatsApi`
- [ ] Base URL configurable via `import.meta.env.VITE_API_BASE`
- [ ] Query param builders for optional filters
- **Status:** not started

### Phase 3: Dashboard pages
- [ ] `src/pages/Dashboard.tsx/svelte/vue` — summary cards + daily chart
- [ ] `src/pages/Requests.tsx/svelte/vue` — paginated table
- [ ] Model filter dropdown (auto-discovered from data)
- **Status:** not started

### Phase 4: Polish
- [ ] Date range picker
- [ ] Export to CSV
- [ ] Loading states, error handling
- **Status:** not started

## Decisions Needed
| Decision | Options | Status |
|----------|---------|--------|
| UI Framework | SvelteKit / Vue 3 / React | ⬜ TBD |
| Chart library | Recharts / Chart.js / D3 / lightweight | ⬜ TBD |
| Styling | CSS modules / Tailwind / UnoCSS | ⬜ TBD |
| Auth | Pass through Basic Auth header? | ⬜ TBD |

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
|       |         |            |
