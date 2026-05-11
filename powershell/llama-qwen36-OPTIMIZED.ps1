# Qwen3.6 35B-A3B Optimized - UD-IQ4_XS
param(
    [double]$Temperature = 0.6,
    [double]$TopP = 0.95,
    [double]$TopK = 20,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.05,
    [int]$ContextLength = 65536,
    [int]$Port = 8001,
    [int]$Threads = 0,
    [switch]$Verbose
)

. .\common.ps1
. .\config.ps1

$paths = $script:CachePaths
New-CacheDirectories -Directories @($paths['Temp'], $paths['LlamaCache'], $paths['HfCache'], $paths['NvidiaCache'])
Setup-Environment -Temp $paths['Temp'] -LlamaCache $paths['LlamaCache'] `
    -HfCache $paths['HfCache'] -NvidiaCache $paths['NvidiaCache']

$ThreadCount = if ($Threads -eq 0) { Get-CpuThreads } else { $Threads }

try { Validate-Port -Port $Port } catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }

$ModelPath = "F:\llama-cache\models--unsloth--Qwen3.6-35B-A3B-GGUF\snapshots\a483e9e6cbd595906af30beda3187c2663a1118c\Qwen3.6-35B-A3B-UD-IQ4_XS.gguf"

if ($Verbose) {
    Format-ConfigHeader -Title "Qwen3.6 35B-A3B (UD-IQ4_XS)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

try {
    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "qwen36-optimized" `
        --n-gpu-layers 99 `
        --n-cpu-moe 14 `
        --reasoning on `
        --jinja `
        --chat-template-file chat_template.jinja `
        -c $ContextLength `
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
        --slot-save-path "$paths['Models']/llamacache" `
        -fa on `
        --cache-type-k q8_0 `
        --cache-type-v turbo4 `
        --cache-ram 8192 `
        --no-mmap `
        -lv 2
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
