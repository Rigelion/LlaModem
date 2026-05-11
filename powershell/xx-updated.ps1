# Qwen3.5 9B Byteshape - Optimized (Updated)
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

# Load paths - bracket notation works for standalone expressions
$paths = $script:CachePaths

# CRITICAL: Assign to temp variables BEFORE using in string contexts
# This avoids PowerShell's string interpolation quirk where $paths['Models']
# inside quotes becomes "@{...}['Models']" instead of the actual value
$modelsPath = $paths['Models']
$tempPath = $paths['Temp']
$llamaCachePath = $paths['LlamaCache']
$hfCachePath = $paths['HfCache']
$nvidiaCachePath = $paths['NvidiaCache']

# Build slot path using Join-Path with the temp variable
$slotPath = Join-Path $modelsPath "llamacache"

# Create directories using temp variables
New-CacheDirectories -Directories @($tempPath, $llamaCachePath, $hfCachePath, $nvidiaCachePath)
Setup-Environment -Temp $tempPath -LlamaCache $llamaCachePath `
    -HfCache $hfCachePath -NvidiaCache $nvidiaCachePath

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
