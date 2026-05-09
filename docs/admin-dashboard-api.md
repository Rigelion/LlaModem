# Admin Dashboard API Documentation

This document describes the API endpoints for the LlaModem admin dashboard. The dashboard displays model status, performance metrics, and allows parameter editing.

## API Overview

LlaModem has two API endpoint sets:

| Endpoint Set | Purpose | Frontend Project |
|--------------|---------|------------------|
| `/admin/stats/*` | Usage statistics, cost comparison, recent requests | `fe-stats` (existing) |
| `/admin/dashboard/*` | Model status, metrics, start/stop control, parameter editing | (to be integrated) |

This document covers the **admin dashboard API** (`/admin/dashboard/*`).

## Base URL
All endpoints are prefixed with `/admin/dashboard/`

## Frontend Integration

The existing frontend project (`fe-stats`) uses `/admin/stats/*` endpoints for usage monitoring:

```bash
fe-stats/
├── src/
│   ├── api/
│   │   └── statsApi.ts      # Existing stats API client
│   ├── hooks/
│   │   ├── useDailyUsage.ts # Existing usage hook
│   │   └── useRecentRequests.ts
│   ├── types/
│   │   └── stats.ts         # Existing stats types
│   ├── components/
│   │   └── dashboard/
│   │       ├── SummaryCards.tsx
│   │       └── CostComparison.tsx
│   ├── store/
│   │   └── statsStore.ts    # Zustand store
│   └── routes/
│       ├── index.tsx
│       ├── dashboard.tsx
│       └── requests.tsx
```

To integrate the admin dashboard API:

1. **Add API client**: Create `src/api/dashboardApi.ts` (same pattern as `statsApi.ts`)
2. **Add types**: Create `src/types/dashboard.ts` (types defined below)
3. **Add hooks**: Create `src/hooks/useModels.ts` (same pattern as `useDailyUsage.ts`)
4. **Add routes**: Create `src/routes/admin/models.tsx` for model management
5. **Use existing components**: Leverage `shadcn/ui` components from `src/components/ui/`
6. **Update store**: Add admin dashboard state to `src/store/statsStore.ts`

---

## Implementation Status

| Phase | Status | Description |
|-------|--------|-------------|
| 1 | ✅ Complete | Data model & API design |
| 2 | ✅ Complete | Metrics collection service |
| 3 | ✅ Complete | Parameter persistence |
| 4 | 🔄 In Progress | Frontend integration (`fe-stats`) |

---

## 1. Get All Models Summary

**GET /admin/dashboard/models**

Returns a list of all configured models with their current status and performance metrics.

### Response: `ModelDashboardItem[]`

```typescript
interface ModelDashboardItem {
  name: string;              // Model name (e.g., "qwen-smart")
  status: "running_active" | "running_idle" | "stopped";
  currentTokensPerSecond: number | null;  // Rolling 30s window, null if not running
  averageTokensPerSession: number | null; // Total tokens / session time, null if no session
  parameters: {
    temperature?: number;
    topP?: number;
    topK?: number;
    minP?: number;
    presencePenalty?: number;
    repetitionPenalty?: number;
  };
  scriptPath: string;
  processId: number | null;
  startedAt: string | null;   // ISO 8601 timestamp
  lastRequestAt: string | null;
  isHealthy: boolean | null;  // null if not running
  lastError: string | null;
}
```

### Example Response
```json
[
  {
    "name": "qwen-smart",
    "status": "running_active",
    "currentTokensPerSecond": 42.5,
    "averageTokensPerSession": 38.2,
    "parameters": {
      "temperature": 0.7,
      "topP": 0.9,
      "presencePenalty": 1.1
    },
    "scriptPath": "/path/to/qwen-smart-start.ps1",
    "processId": 12345,
    "startedAt": "2026-05-09T10:30:00Z",
    "lastRequestAt": "2026-05-09T11:45:23Z",
    "isHealthy": true,
    "lastError": null
  },
  {
    "name": "qwen-fast",
    "status": "stopped",
    "currentTokensPerSecond": null,
    "averageTokensPerSession": 55.3,
    "parameters": {
      "temperature": 0.5,
      "topP": 0.8
    },
    "scriptPath": "/path/to/qwen-fast-start.ps1",
    "processId": null,
    "startedAt": null,
    "lastRequestAt": "2026-05-09T09:15:00Z",
    "isHealthy": null,
    "lastError": null
  }
]
```

---

## 2. Get Model Details

**GET /admin/dashboard/models/{name}**

Returns detailed information for a specific model.

### Path Parameters
- `name` (string): Model name (e.g., "qwen-smart")

### Response: `ModelDashboardItem`
Same structure as above, but for a single model.

### Example Response
```json
{
  "name": "qwen-smart",
  "status": "running_active",
  "currentTokensPerSecond": 42.5,
  "averageTokensPerSession": 38.2,
  "parameters": {
    "temperature": 0.7,
    "topP": 0.9,
    "topK": 40,
    "minP": 0.5,
    "presencePenalty": 1.1,
    "repetitionPenalty": 1.2
  },
  "scriptPath": "/Users/rigel/.qwen/qwen-smart-start.ps1",
  "processId": 12345,
  "startedAt": "2026-05-09T10:30:00Z",
  "lastRequestAt": "2026-05-09T11:45:23Z",
  "isHealthy": true,
  "lastError": null
}
```

---

## 3. Start Model

**POST /admin/dashboard/models/{name}/start**

Starts the specified model. Optionally overrides parameters.

### Path Parameters
- `name` (string): Model name

### Request Body (Optional)
```typescript
interface StartModelRequest {
  temperature?: number;
  topP?: number;
  topK?: number;
  minP?: number;
  presencePenalty?: number;
  repetitionPenalty?: number;
}
```

### Example Request
```json
{
  "temperature": 0.75,
  "topP": 0.95
}
```

### Success Response (200 OK)
```json
{
  "message": "Model 'qwen-smart' started successfully",
  "processId": 12345,
  "startedAt": "2026-05-09T11:45:30Z"
}
```

### Error Responses

**404 Not Found**
```json
{
  "error": "ModelNotFound",
  "message": "Model 'nonexistent' not found in configuration"
}
```

**503 Service Unavailable**
```json
{
  "error": "StartFailed",
  "message": "Failed to start model 'qwen-smart': Health check failed after 300s"
}
```

---

## 4. Stop Model

**POST /admin/dashboard/models/{name}/stop**

Stops the specified model gracefully.

### Path Parameters
- `name` (string): Model name

### Success Response (200 OK)
```json
{
  "message": "Model 'qwen-smart' stopped successfully",
  "processId": 12345,
  "stoppedAt": "2026-05-09T11:50:00Z"
}
```

### Error Responses

**404 Not Found**
```json
{
  "error": "ModelNotFound",
  "message": "Model 'nonexistent' not found in configuration"
}
```

**400 Bad Request**
```json
{
  "error": "NoActiveModel",
  "message": "No active model to stop"
}
```

---

## 5. Update Model Parameters

**PUT /admin/dashboard/models/{name}/params**

Updates the 6 editable parameters for a model. Changes are persisted to `dashboard_params.json` and require a restart to take effect.

### Path Parameters
- `name` (string): Model name

### Request Body
```typescript
interface UpdateParamsRequest {
  temperature?: number;
  topP?: number;
  topK?: number;
  minP?: number;
  presencePenalty?: number;
  repetitionPenalty?: number;
}
```

### Example Request
```json
{
  "temperature": 0.8,
  "topP": 0.95,
  "presencePenalty": 1.2
}
```

### Success Response (200 OK)
```json
{
  "message": "Parameters updated successfully",
  "restartRequired": true,
  "updatedParams": {
    "temperature": 0.8,
    "topP": 0.95,
    "presencePenalty": 1.2
  }
}
```

### Error Responses

**404 Not Found**
```json
{
  "error": "ModelNotFound",
  "message": "Model 'nonexistent' not found in configuration"
}
```

**400 Bad Request**
```json
{
  "error": "InvalidParameters",
  "message": "temperature must be between 0 and 2"
}
```

---

## 6. Get Model Health

**GET /admin/dashboard/models/{name}/health**

Returns the current health check status for a running model.

### Path Parameters
- `name` (string): Model name

### Success Response (200 OK)
```json
{
  "isHealthy": true,
  "healthUrl": "http://localhost:8001/health",
  "lastCheckedAt": "2026-05-09T11:45:25Z",
  "responseTimeMs": 45
}
```

### Error Responses

**404 Not Found**
```json
{
  "error": "ModelNotFound",
  "message": "Model 'nonexistent' not found in configuration"
}
```

**400 Bad Request**
```json
{
  "error": "ModelNotRunning",
  "message": "Model 'qwen-smart' is not running"
}
```

---

## 7. Real-Time Updates (Optional)

For real-time status updates, the frontend can use Server-Sent Events (SSE) or WebSocket.

### SSE Endpoint
**GET /admin/dashboard/models/events**

Returns a stream of model status changes.

### Event Format
```json
{
  "event": "status_changed",
  "timestamp": "2026-05-09T11:45:30Z",
  "data": {
    "name": "qwen-smart",
    "status": "running_active",
    "currentTokensPerSecond": 42.5
  }
}
```

---

## Status Values

| Status | Meaning |
|--------|---------|
| `running_active` | Model is running and actively processing requests |
| `running_idle` | Model is running but has not received requests in the last 10 seconds |
| `stopped` | Model is not running |

---

## Metrics Calculation

### Current Tokens Per Second
- **Window**: Rolling 30-second window
- **Scope**: Overall tokens (sum of prompt + completion tokens)
- **Calculation**: `tokens_in_window / 30`
- **Null**: If model is stopped or not running

### Average Tokens Per Session
- **Session**: From model start until stop (explicit or timeout)
- **Calculation**: `total_tokens / total_session_time_seconds`
- **Null**: If no session has completed

---

## Error Codes

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `ModelNotFound` | 404 | Model not in configuration |
| `StartFailed` | 503 | Model failed to start (health check, VRAM, etc.) |
| `NoActiveModel` | 400 | No model to stop |
| `InvalidParameters` | 400 | Parameter validation failed |
| `ModelNotRunning` | 400 | Operation requires running model |

---

## Frontend Implementation Notes

### API Client Integration

Follow the same pattern as `src/api/statsApi.ts`:

```typescript
// src/api/dashboardApi.ts

const API_BASE = ''

function createDashboardApi() {
  async function get<T>(url: string): Promise<T> {
    const response = await fetch(`${API_BASE}${url}`)
    if (!response.ok) {
      const body = await response.text().catch(() => '')
      throw new Error(`Dashboard API error ${response.status}: ${body}`)
    }
    return response.json() as T
  }

  return {
    async getAllModels(): Promise<ModelDashboardItem[]> {
      return get('/admin/dashboard/models')
    },

    async getModel(name: string): Promise<ModelDashboardItem> {
      return get(`/admin/dashboard/models/${name}`)
    },

    async startModel(name: string, params?: StartModelRequest): Promise<StartModelResult> {
      const response = await fetch(`${API_BASE}/admin/dashboard/models/${name}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(params || {}),
      })
      return response.json() as StartModelResult
    },

    async stopModel(name: string): Promise<StopModelResult> {
      const response = await fetch(`${API_BASE}/admin/dashboard/models/${name}/stop`, {
        method: 'POST',
      })
      return response.json() as StopModelResult
    },

    async updateParams(name: string, params: UpdateModelParamsRequest): Promise<ParameterUpdateResponse> {
      const response = await fetch(`${API_BASE}/admin/dashboard/models/${name}/params`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(params),
      })
      return response.json() as ParameterUpdateResponse
    },

    async getHealth(name: string): Promise<HealthCheckResponse> {
      return get(`/admin/dashboard/models/${name}/health`)
    },
  }
}

export const dashboardApi = createDashboardApi()
```

### Query Hooks

Follow the same pattern as `src/hooks/useDailyUsage.ts`:

```typescript
// src/hooks/useModels.ts

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { dashboardApi } from '@/api/dashboardApi'
import type { ModelDashboardItem } from '@/types/dashboard'

const MODELS_KEY = ['dashboard', 'models'] as const

export function useModels() {
  return useQuery<ModelDashboardItem[]>({
    queryKey: MODELS_KEY,
    queryFn: () => dashboardApi.getAllModels(),
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchInterval: 10 * 1000, // refetch every 10s
    refetchOnWindowFocus: true,
  })
}

export function useModel(name: string) {
  return useQuery<ModelDashboardItem>({
    queryKey: [...MODELS_KEY, name],
    queryFn: () => dashboardApi.getModel(name),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 10 * 1000,
    enabled: !!name,
  })
}
```

### Zustand Store Integration

Add admin dashboard state to `src/store/statsStore.ts`:

```typescript
// src/store/statsStore.ts

import { create } from 'zustand'

export interface StatsStore {
  // Existing stats state...
  model: string | null
  setModel: (model: string | null) => void
  days: number
  setDays: (days: number) => void

  // New admin dashboard state
  selectedModel: string | null
  setSelectedModel: (model: string | null) => void
  refreshModels: () => void
}

export const useStatsStore = create<StatsStore>((set, get) => ({
  // Existing state...
  model: null,
  setModel: (model) => set({ model }),
  days: 30,
  setDays: (days) => set({ days }),

  // New state
  selectedModel: null,
  setSelectedModel: (model) => set({ selectedModel: model }),
  refreshModels: () => {
    // Trigger query refetch via TanStack Query
    const queryClient = get().queryClient
    queryClient?.invalidateQueries({ queryKey: ['dashboard', 'models'] })
  },
}))
```

### Polling Strategy

- **Poll `/admin/dashboard/models`**: Every 10 seconds via `refetchInterval`
- **Poll current t/s**: Every 1 second if real-time needed (use `refetchInterval: 1000`)
- **On-demand refresh**: Use `refetch()` from useQuery hook for manual refresh

### Parameter Validation

- `temperature`: 0.0 - 2.0
- `topP`: 0.0 - 1.0
- `topK`: 1 - 100 (or null for default)
- `minP`: 0.0 - 1.0
- `presencePenalty`: -2.0 to 2.0
- `repetitionPenalty`: 0.0 - 3.0

### UI Components Needed

Use the same shadcn/ui components as the existing frontend:

1. **Model List Card**: `Card` + `CardContent` from `@/components/ui/card`
2. **Model Detail Panel**: Collapsible `Card` with parameter grid
3. **Parameter Editor**: `Input` + `Label` from `@/components/ui/input`, `@/components/ui/label`
4. **Start/Stop Buttons**: `Button` from `@/components/ui/button` with Lucide icons
5. **Metrics Display**: Visual indicators (progress bars, color-coded badges)
6. **Status Badges**: Color-coded badges for `running_active`, `running_idle`, `stopped`

### Example Component Structure

```tsx
// src/components/admin/ModelCard.tsx

import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Play, Square, RefreshCw } from 'lucide-react'
import { useModel } from '@/hooks/useModels'
import { useStatsStore } from '@/store/statsStore'

export function ModelCard({ name }: { name: string }) {
  const { data: model, isLoading } = useModel(name)
  const { setSelectedModel } = useStatsStore()

  if (isLoading) return <Card><CardContent>Loading...</CardContent></Card>
  if (!model) return null

  return (
    <Card>
      <CardContent className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">{model.name}</h3>
          <StatusBadge status={model.status} />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <Metric value={model.currentTokensPerSecond} label="Current t/s" />
          <Metric value={model.averageTokensPerSession} label="Avg t/s" />
        </div>

        <div className="space-y-2">
          <Label>Parameters</Label>
          <ParameterGrid params={model.parameters} />
        </div>

        <div className="flex gap-2">
          {model.status === 'stopped' ? (
            <Button onClick={() => startModel(name)}>
              <Play className="h-4 w-4" /> Start
            </Button>
          ) : (
            <Button onClick={() => stopModel(name)} variant="destructive">
              <Square className="h-4 w-4" /> Stop
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
```

### Routing

Add a new route for admin dashboard:

```tsx
// src/routes/admin/models.tsx

import { createFileRoute } from '@tanstack/react-router'
import { ModelList } from '@/components/admin/ModelList'

export const Route = createFileRoute('/admin/models')({
  component: AdminModelsPage,
})

function AdminModelsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold">Model Management</h1>
      <ModelList />
    </div>
  )
}
```

---

## Backend Implementation Files

| File | Purpose |
|------|----------|
| `Services/ModelMetricsService.cs` | Metrics collection with rolling 30s window |
| `Services/DashboardService.cs` | All dashboard business logic |
| `Models/DashboardModels.cs` | DTOs for API responses |
| `Middleware/ResponseUsageMiddleware.cs` | Hooks into `ModelMetricsService` |
| `EndpointSetup.cs` | Maps all `/admin/dashboard/*` endpoints |
| `Program.cs` | Service registration |

## Frontend Integration Plan

| Task | File | Status |
|------|------|--------|
| Add API client | `fe-stats/src/api/dashboardApi.ts` | To do |
| Add types | `fe-stats/src/types/dashboard.ts` | To do |
| Add hooks | `fe-stats/src/hooks/useModels.ts` | To do |
| Add route | `fe-stats/src/routes/admin/models.tsx` | To do |
| Update store | `fe-stats/src/store/statsStore.ts` | To do |

## Key Classes

### `ModelMetricsService`
- Tracks tokens in rolling 30s window
- Tracks session start/end
- Calculates average t/s per session
- Hooked into `ResponseUsageMiddleware`

### `DashboardService`
- `GetAllModelsAsync()` - List models with status/metrics
- `GetModelAsync()` - Single model details
- `StartModelAsync()` - Start with optional param overrides
- `StopModelAsync()` - Stop active model
- `UpdateParamsAsync()` - Update & persist 6 params
- `GetHealthAsync()` - Health check status

---

## File Locations

- **Config**: `appsettings.json` (read-only for dashboard)
- **Runtime Params**: `dashboard_params.json` (writable by dashboard)
- **Metrics**: In-memory only (not persisted)

## Parameter Persistence (`dashboard_params.json`)

### Format
```json
{
  "modelName": {
    "Temperature": 0.7,
    "TopP": 0.9,
    "TopK": 40,
    "MinP": 0.5,
    "PresencePenalty": 1.1,
    "RepetitionPenalty": 1.2
  }
}
```

### Lifecycle
1. **First Update**: When `PUT /admin/dashboard/models/{name}/params` is called, the file is created if it doesn't exist
2. **Subsequent Updates**: The file is overwritten with the complete merged parameters
3. **Start**: When `POST /admin/dashboard/models/{name}/start` is called, persisted params are loaded from the file
4. **Persistence**: Changes persist across restarts (restart required for changes to take effect)

### File Location
- Path: `{appDir}/dashboard_params.json`
- Where `{appDir}` is the application's base directory (e.g., `bin/Debug/net10.0-windows/`)

### Example
```bash
# After updating params, check the file
$ cat bin/Debug/net10.0-windows/dashboard_params.json
{
  "qwen-smart": {
    "Temperature": 0.7,
    "TopP": 0.9,
    "TopK": 40,
    "MinP": 0.5,
    "PresencePenalty": 1.1,
    "RepetitionPenalty": 1.2
  }
}
```

---

## TypeScript Types

Add these types to `src/types/dashboard.ts`:

```typescript
// src/types/dashboard.ts

export interface ModelDashboardItem {
  name: string;
  status: 'running_active' | 'running_idle' | 'stopped';
  currentTokensPerSecond: number | null;
  averageTokensPerSession: number | null;
  parameters: {
    temperature?: number;
    topP?: number;
    topK?: number;
    minP?: number;
    presencePenalty?: number;
    repetitionPenalty?: number;
  };
  scriptPath: string;
  processId: number | null;
  startedAt: string | null;
  lastRequestAt: string | null;
  isHealthy: boolean | null;
  lastError: string | null;
}

export interface StartModelRequest {
  temperature?: number;
  topP?: number;
  topK?: number;
  minP?: number;
  presencePenalty?: number;
  repetitionPenalty?: number;
}

export interface StartModelResult {
  success: boolean;
  message: string;
  processId: number | null;
  startedAt: string | null;
}

export interface StopModelResult {
  success: boolean;
  message: string;
  processId: number | null;
  stoppedAt: string | null;
}

export interface UpdateModelParamsRequest {
  temperature?: number;
  topP?: number;
  topK?: number;
  minP?: number;
  presencePenalty?: number;
  repetitionPenalty?: number;
}

export interface ParameterUpdateResponse {
  message: string;
  restartRequired: boolean;
  updatedParams: {
    temperature: number;
    topP: number;
    topK: number;
    minP: number;
    presencePenalty: number;
    repetitionPenalty: number;
  };
}

export interface HealthCheckResponse {
  isHealthy: boolean;
  healthUrl: string;
  lastCheckedAt: string;
  responseTimeMs: number;
}

export interface DashboardApi {
  getAllModels(): Promise<ModelDashboardItem[]>;
  getModel(name: string): Promise<ModelDashboardItem>;
  startModel(name: string, params?: StartModelRequest): Promise<StartModelResult>;
  stopModel(name: string): Promise<StopModelResult>;
  updateParams(name: string, params: UpdateModelParamsRequest): Promise<ParameterUpdateResponse>;
  getHealth(name: string): Promise<HealthCheckResponse>;
}
```

## Example Frontend Code (TypeScript)

### React Query Hooks

```typescript
// src/hooks/useModels.ts

import { useQuery } from '@tanstack/react-query'
import { dashboardApi } from '@/api/dashboardApi'

export function useModels() {
  return useQuery({
    queryKey: ['dashboard', 'models'],
    queryFn: () => dashboardApi.getAllModels(),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 10 * 1000,
  })
}

export function useModel(name: string) {
  return useQuery({
    queryKey: ['dashboard', 'models', name],
    queryFn: () => dashboardApi.getModel(name),
    enabled: !!name,
  })
}
```

### Component Usage

```tsx
// src/components/admin/ModelList.tsx

import { useModels } from '@/hooks/useModels'
import { ModelCard } from '@/components/admin/ModelCard'

export function ModelList() {
  const { data: models, isLoading } = useModels()

  if (isLoading) return <p>Loading models...</p>

  return (
    <div className="grid gap-4">
      {models?.map((model) => (
        <ModelCard key={model.name} model={model} />
      ))}
    </div>
  )
}
```

### Parameter Editing Form

```tsx
// src/components/admin/ParameterForm.tsx

import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi } from '@/api/dashboardApi'

export function ParameterForm({ modelName }: { modelName: string }) {
  const [params, setParams] = useState<UpdateModelParamsRequest>({})
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => dashboardApi.updateParams(modelName, params),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'models'] })
    },
  })

  return (
    <form onSubmit={(e) => {
      e.preventDefault()
      mutation.mutate()
    }}>
      <div className="space-y-4">
        <div>
          <Label htmlFor="temperature">Temperature</Label>
          <Input
            id="temperature"
            type="number"
            step="0.1"
            min="0"
            max="2"
            value={params.temperature ?? ''}
            onChange={(e) => setParams(p => ({ ...p, temperature: parseFloat(e.target.value) }))}
          />
        </div>
        {/* More parameter inputs... */}
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving...' : 'Save Parameters'}
        </Button>
      </div>
    </form>
  )
}
```

### Start/Stop Actions

```tsx
// src/components/admin/ModelControls.tsx

import { Button } from '@/components/ui/button'
import { Play, Square } from 'lucide-react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { dashboardApi } from '@/api/dashboardApi'

export function ModelControls({ modelName }: { modelName: string }) {
  const queryClient = useQueryClient()

  const startMutation = useMutation({
    mutationFn: () => dashboardApi.startModel(modelName),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dashboard', 'models'] }),
  })

  const stopMutation = useMutation({
    mutationFn: () => dashboardApi.stopModel(modelName),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dashboard', 'models'] }),
  })

  return (
    <div className="flex gap-2">
      <Button
        onClick={() => startMutation.mutate()}
        disabled={startMutation.isPending}
      >
        <Play className="h-4 w-4 mr-2" /> Start
      </Button>
      <Button
        onClick={() => stopMutation.mutate()}
        variant="destructive"
        disabled={stopMutation.isPending}
      >
        <Square className="h-4 w-4 mr-2" /> Stop
      </Button>
    </div>
  )
}
```

### Status Badge Component

```tsx
// src/components/admin/StatusBadge.tsx

import { cn } from '@/lib/utils'

export function StatusBadge({ status }: { status: ModelDashboardItem['status'] }) {
  const styles = {
    running_active: 'bg-green-100 text-green-800',
    running_idle: 'bg-yellow-100 text-yellow-800',
    stopped: 'bg-gray-100 text-gray-800',
  }

  return (
    <span className={cn(
      'px-2 py-1 rounded-full text-xs font-medium',
      styles[status]
    )}>
      {status.replace('_', ' ')}
    </span>
  )
}
```

### Error Handling

```tsx
// src/hooks/useModels.ts

export function useModels() {
  return useQuery({
    queryKey: ['dashboard', 'models'],
    queryFn: () => dashboardApi.getAllModels(),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 10 * 1000,
    retry: (failureCount, error) => {
      // Don't retry 404 errors
      if (error instanceof Error && error.message.includes('404')) {
        return false
      }
      return failureCount < 3
    },
  })
}
```
