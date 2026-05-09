# Frontend OpenAPI Consumer Implementation - TODO

## Status: Phase 1-3 Complete ✅ | Ready for Backend Integration 🚧

---

## Completed Phases

### ✅ Phase 1: Package Installation
- Installed `@hey-api/openapi-ts`, `@hey-api/vite-plugin`, `@hey-api/client-fetch`

### ✅ Phase 2: Vite Plugin Configuration
- Created `fe-stats/.env-local` with `VITE_OPENAPI_URL`
- Created `fe-stats/openapi-ts.config.ts`
- Updated `fe-stats/vite.config.ts` with `heyApiPlugin`

### ✅ Phase 3: Generate Types
- Build successful with placeholder types
- Created `src/client/operations.ts` with manual type wrappers
- Auth headers handled via `VITE_AUTH_USERNAME`/`VITE_AUTH_PASSWORD`

---

## Backend Status

### Current State
- ✅ OpenAPI JSON available at `/openapi/v1.json`
- ⚠️ Response schemas are `unknown` (manual types needed)
- ✅ Scalar UI at `/scalar`

### Backend Changes Needed (Not Completed)
- ❌ Add proper OpenAPI response schemas to endpoints
- ❌ Add request body schemas for POST/PUT endpoints
- ❌ Add query parameter definitions

**Note:** The backend OpenAPI schemas are complex. For now, we'll use manual type wrappers in the frontend.

---

## Next Steps: Phase 4 - Frontend Migration

### Current State
- ✅ Hey-API integration configured
- ✅ Build passes with placeholder types
- 🚧 Ready to replace API clients with generated operations

### Migration Plan (Option C: One Hook at a Time)

#### Step 1: Update `useModels.ts`
```typescript
// Before
import type { ModelDashboardItem } from '@/types/dashboard'
import { dashboardApi } from '@/api/dashboardApi'

// After
import type { ModelDashboardItem } from '@/types/dashboard'
import { getAllModelsOperation } from '@/client'
```

#### Step 2: Update `useDailyUsage.ts`
```typescript
// Before
import { statsApi } from '@/api/statsApi'

// After
import { getDailyUsageOperation } from '@/client'
```

#### Step 3: Update `useRecentRequests.ts`
```typescript
// Before
import { statsApi } from '@/api/statsApi'

// After
import { getRecentRequestsOperation } from '@/client'
```

#### Step 4: Update `useCostComparison.ts`
```typescript
// Before
import { statsApi } from '@/api/statsApi'

// After
import { getCostComparisonOperation } from '@/client'
```

---

## Files to Update

### Already Created ✅
- `fe-stats/.env-local`
- `fe-stats/openapi-ts.config.ts`
- `fe-stats/src/client/operations.ts`
- `fe-stats/src/client/index.ts`
- `fe-stats/src/client/sdk.gen.ts` (placeholder)
- `fe-stats/src/client/types.gen.ts` (placeholder)
- `fe-stats/src/client/client.gen.ts` (placeholder)
- `fe-stats/src/client/core/index.ts` (placeholder)

### To Update 🚧
- `src/hooks/useModels.ts`
- `src/hooks/useDailyUsage.ts`
- `src/hooks/useRecentRequests.ts`
- `src/hooks/useCostComparison.ts`
- `src/api/statsApi.ts` (remove after migration)
- `src/api/dashboardApi.ts` (remove after migration)

---

## Testing Checklist

Before starting migration:
- [ ] Backend running at `http://localhost:9000`
- [ ] `curl http://localhost:9000/openapi/v1.json` returns valid JSON
- [ ] `npm run build` succeeds
- [ ] `npm run dev` starts without errors

After migration:
- [ ] All hooks use `@/client` operations
- [ ] No TypeScript errors
- [ ] All pages render correctly
- [ ] API calls return expected data
- [ ] Tests pass

---

## Rollback Plan

If issues arise:
```bash
cd fe-stats
# Remove generated types
rm -rf src/client/

# Restore old API clients
git checkout HEAD -- src/api/

# Revert config changes
git checkout HEAD -- vite.config.ts
rm openapi-ts.config.ts
rm .env-local
```

---

## Resources

- [@hey-api/openapi-ts docs](https://heyapi.dev/openapi-ts)
- [@hey-api/vite-plugin](https://heyapi.dev/openapi-ts/configuration/vite)
- Backend: `~/RiderProjects/llamodem`
- Frontend: `~/RiderProjects/llamodem-FE/fe-stats`
