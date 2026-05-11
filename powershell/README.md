# Llama-Server Scripts - Optimized

## Optimizations Applied

1. **Centralized Configuration** - Common paths and functions in `common.ps1`
2. **Auto Thread Detection** - Scripts now auto-detect CPU cores (set `$Threads = 0`)
3. **Configurable Ports** - Each script can use different ports to avoid conflicts
4. **Error Handling** - Try/catch blocks with proper exit codes
5. **Port Validation** - Checks if port is already in use before starting
6. **Reduced Verbosity** - Use `-Verbose` flag to see full config output
7. **Consistent Parameters** - Unified llama-server flags across all scripts
8. **DRY Principle** - No duplicate environment variable setup

## Usage

### Basic Usage
```powershell
.\qwen35-35B-A3B-BYTESHAPE.ps1
```

### With Custom Parameters
```powershell
.\qwen35-35B-A3B-BYTESHAPE.ps1 -Port 8002 -ContextLength 131072 -Verbose
```

### Available Parameters (all scripts)
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Temperature | double | 0.6 | Sampling temperature |
| TopP | double | 0.95 | Nucleus sampling |
| TopK | int | 20 | Top-K sampling |
| MinP | double | 0.0 | Minimum P |
| PresencePenalty | double | 0.0 | Presence penalty |
| RepetitionPenalty | double | 1.00 | Repetition penalty |
| ContextLength | int | varies | Context window size |
| Port | int | 8001 | Server port |
| Threads | int | 0 | CPU threads (0=auto) |
| Verbose | switch | false | Show full config |

## Available Scripts

| Script | Model | Quantization | Context | Threads |
|--------|-------|--------------|---------|---------|
| qwen35-35B-A3B-BYTESHAPE.ps1 | Qwen3.5 35B | IQ3_S | 100K | Auto |
| llama-qwen36-OPTIMIZED.ps1 | Qwen3.6 35B | UD-IQ4_XS | 65K | Auto |
| qwen3-CODER-30B-A3B-BYTESHAPE.ps1 | Qwen3-Coder 30B | IQ3_S | 202K | Auto |
| qwen35-9B-Byteshape.ps1 | Qwen3.5 9B | Q5_K_S | 65K | Auto |
| llama-qwen36-SMART.ps1 | Qwen3.6 35B | UD-Q4_K_M | 80K | Auto |

## Changing Base Paths

Edit `config.ps1` to change all paths at once:
```powershell
$script:CachePaths = @{
    Temp = "D:\Temp"
    LlamaCache = "D:\llama-cache"
    HfCache = "D:\hf-cache"
    NvidiaCache = "D:\NVIDIA-cache"
    Models = "D:\models"
}
```

## Troubleshooting

- **Port in use**: Change `-Port` to a different value
- **Low performance**: Check thread count with `Get-CpuThreads`
- **OOM errors**: Reduce `--context-length` or use a smaller model
