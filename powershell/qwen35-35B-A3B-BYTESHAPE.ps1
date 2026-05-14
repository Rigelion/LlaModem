# Qwen3.5 35B-A3B Byteshape - Optimized
param(
    [double]$Temperature = 0.3,
    [double]$TopP = 0.95,
    [double]$TopK = 20,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.08,
    [int]$ContextLength = 60000,
    [int]$Port = 8001,
    [int]$Threads = 0,  # 0 = auto-detect
    [switch]$Verbose
)

# Import common functions
. .\common.ps1
. .\config.ps1

# Setup paths - use dot notation for class properties
$paths = $script:CachePaths
New-CacheDirectories -Directories @(
    $paths.Temp, $paths.LlamaCache, $paths.HfCache, $paths.NvidiaCache
)
Setup-Environment -Temp $paths.Temp -LlamaCache $paths.LlamaCache `
    -HfCache $paths.HfCache -NvidiaCache $paths.NvidiaCache

# Auto-detect threads
$ThreadCount = if ($Threads -eq 0) { Get-CpuThreads } else { $Threads }

# Validate port
try { Validate-Port -Port $Port } catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }

# Model path (optimized: using IQ3 for better speed/quality balance)
$ModelPath = "F:\llama-cache\models--byteshape--Qwen3.5-35B-A3B-GGUF\snapshots\07a18b090d818887a2b5ac6f248c7b538648f416\Qwen3.5-35B-A3B-IQ3_S-3.01bpw.gguf"

if ($Verbose) {
    Format-ConfigHeader -Title "Qwen3.5 35B-A3B (IQ3_S)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

# Run llama-server
try {
    # Build slot path using dot notation
    $slotPath = Join-Path $paths.Models "llamacache"

    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "qwen35-35b" `
        --n-gpu-layers 99 `
        --n-cpu-moe 12 `
        -c $ContextLength `
        --jinja `
        -n 4096 `
        --no-context-shift `
        --batch-size 2048 `
        --ubatch-size 1024 `
        --parallel 1 `
        --threads $ThreadCount `
        --temp $Temperature `
        --top-p $TopP `
        --top-k $TopK `
        --min-p $MinP `
        --repeat-penalty $RepetitionPenalty `
        --presence-penalty $PresencePenalty `
        --slot-save-path $slotPath `
        --reasoning on `
        -fa on `
        --cache-ram 8192 `
        --no-mmap `
        -lv 2
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
