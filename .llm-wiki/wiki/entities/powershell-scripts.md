# PowerShell Model Launch Scripts

**Location**: `powershell/` directory  
**Purpose**: Start `llama-server` instances for each configured model

## Script Template

All scripts follow this structure with `[decimal]` parameter types:

```powershell
param(
    [decimal]$Temperature = 0.6,      # Changed from double
    [decimal]$TopP = 0.95,            # Changed from double
    [decimal]$TopK = 20,              # Changed from double
    [decimal]$MinP = 0.0,             # Changed from double
    [decimal]$PresencePenalty = 0.00, # Changed from double
    [decimal]$RepetitionPenalty = 1.05 # Changed from double
)
```

## Parameter Precision

**Changed from `[double]` to `[decimal]`** for exact 2-decimal precision compatibility with llama-server.

Example:
- `double`: `0.6` → serialized as `0.6000000000000001` (floating point error)
- `decimal`: `0.6m` → serialized as `0.6` (exact precision)

## Common Scripts

| Script | Model | Context Length |
|--------|-------|----------------|
| `llama-qwen36-SMART.ps1` | Qwen3.6 35B-A3B | 80K |
| `llama-qwen36-OPTIMIZED.ps1` | Qwen3.6 35B-A3B (opt) | 65K |
| `qwen35-35B-A3B-BYTESHAPE.ps1` | Qwen3.5 35B-A3B | 100K |
| `qwen35-9B-Byteshape.ps1` | Qwen3.5 9B | 65K |

## Related Components

- [[Services/ModelProxyHandler]] — Loads params on each request
- [[Config/architecture]] — StartScript configuration via %VAR% syntax