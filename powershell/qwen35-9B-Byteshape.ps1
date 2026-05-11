# Qwen3.5 9B Byteshape - Optimized
param(
    [double]$Temperature = 0.6,
    [double]$TopP = 0.95,
    [double]$TopK = 20,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.00,
    [int]$ContextLength = 65536,
    [int]$Port = 8001,
    [int]$Threads = 0,
    [switch]$Verbose
)

. .\common.ps1
. .\config.ps1

# Load paths - use dot notation for class properties
$paths = $script:CachePaths

# Build slot path using dot notation ($paths.Models, not $paths['Models'])
$slotPath = Join-Path $paths.Models "llamacache"

New-CacheDirectories -Directories @($paths.Temp, $paths.LlamaCache, $paths.HfCache, $paths.NvidiaCache)
Setup-Environment -Temp $paths.Temp -LlamaCache $paths.LlamaCache `
    -HfCache $paths.HfCache -NvidiaCache $paths.NvidiaCache

$ThreadCount = if ($Threads -eq 0) { Get-CpuThreads } else { $Threads }

try { Validate-Port -Port $Port } catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }

$ModelPath = "F:\llama-cache\models--byteshape--Qwen3.5-9B-GGUF\snapshots\c0a342e97e5b33fdee725226f7f559426a675968\Qwen3.5-9B-Q5_K_S-4.60bpw.69bpw.gguf"

if ($Verbose) {
    Format-ConfigHeader -Title "Qwen3.5 9B (Q5_K_S)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

try {
    llama-server `
        --model $ModelPath `
        --slot-save-path $slotPath `
        --alias "qwen35-9b" `
        --port $Port `
        -c $ContextLength `
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
        --reasoning on `
        -fa on `
        -lv 2
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
