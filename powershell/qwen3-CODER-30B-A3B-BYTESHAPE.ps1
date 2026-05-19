# Qwen3-Coder 30B-A3B Byteshape - Optimized
param(
    [decimal]$Temperature = 0.4,
    [decimal]$TopP = 0.95,
    [decimal]$TopK = 20,
    [decimal]$MinP = 0.0,
    [decimal]$PresencePenalty = 0.00,
    [decimal]$RepetitionPenalty = 1.00,
    [int]$ContextLength = 60000,
    [int]$Port = 8001,
    [int]$Threads = 0,
    [switch]$Verbose
)

. .\common.ps1
. .\config.ps1

# Setup paths - use dot notation for class properties
$paths = $script:CachePaths
New-CacheDirectories -Directories @($paths.Temp, $paths.LlamaCache, $paths.HfCache, $paths.NvidiaCache)
Setup-Environment -Temp $paths.Temp -LlamaCache $paths.LlamaCache `
    -HfCache $paths.HfCache -NvidiaCache $paths.NvidiaCache

$ThreadCount = if ($Threads -eq 0) { Get-CpuThreads } else { $Threads }

try { Validate-Port -Port $Port } catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }

$ModelPath = "F:\llama-cache\models--byteshape--Qwen3-Coder-30B-A3B-Instruct-GGUF\snapshots\0436a5b1e12e051e8a8568d56a1526734d3ed572\Qwen3-Coder-30B-A3B-Instruct-IQ3_S-2.66bpw.gguf"

if ($Verbose) {
    Format-ConfigHeader -Title "Qwen3-Coder 30B-A3B (IQ3_S)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

try {
    # Build slot path using dot notation
    $slotPath = Join-Path $paths.Models "llamacache"

    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "qwen3-coder" `
        --n-gpu-layers 99 `
        --n-cpu-moe 14 `
        --jinja `
        --no-context-shift `
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
        -lv 2
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
